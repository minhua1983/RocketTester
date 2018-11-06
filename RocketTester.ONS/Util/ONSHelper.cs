using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Reflection;
using System.Collections.Concurrent;
using System.Configuration;
using System.Text;
using ons;
using Redis.Framework;
using Newtonsoft.Json;

namespace RocketTester.ONS
{
    /// <summary>
    /// 事务消息帮助类
    /// 事务消息未提交状态，第一次回查（调用Checker.execute方法）在0~5秒后，之后每5秒会再次回查。消费未提交的话，历经16次4小时46分钟。
    /// 目前采用“多事务生产者单消费者”的策略来初始化事务消息服务
    /// </summary>
    public class ONSHelper
    {
        #region 属性
        /// <summary>
        /// 上游生产者服务类列表
        /// </summary>
        public static List<object> ONSProducerServiceList { get; private set; }

        /// <summary>
        /// 下游消费者服务类列表
        /// </summary>
        public static List<object> ONSConsumerServiceList { get;private set; }

        /// <summary>
        /// 生产者代理类列表
        /// </summary>
        public static List<IONSProducer> ONSProducerList { get; private set; }

        /// <summary>
        /// 消费者代理类列表
        /// </summary>
        public static List<IONSConsumer> ONSConsumerList { get; private set; }

        /// <summary>
        /// executer方法实例字典
        /// </summary>
        public static ConcurrentDictionary<string, MethodInfo> ExecuterMethodDictionary { get; private set; }

        /// <summary>
        /// checker方法实例字典
        /// </summary>
        public static ConcurrentDictionary<string, MethodInfo> CheckerMethodDictionary { get; private set; }
        #endregion

        #region 字段
        //redis地址
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        //获取是否启用ONS
        static string _AliyunOnsIsEnabled = ConfigurationManager.AppSettings["AliyunOnsIsEnabled"] ?? "0";
        //获取RAM控制台消息队列账号的AccessKey
        static string _AliyunOnsAccessKey = ConfigurationManager.AppSettings["AliyunOnsAccessKey"] ?? "";
        //获取RAM控制台消息队列账号的SecretKey
        static string _AliyunOnsSecretKey = ConfigurationManager.AppSettings["AliyunOnsSecretKey"] ?? "";
        //获取当前环境，p代表生产环境production，s代表测试环境staging，d代表开发环境development
        static string _Environment = ConfigurationManager.AppSettings["Environment"] ?? "p";
        //获取当前应用的别名
        static string _ApplicationAlias = ConfigurationManager.AppSettings["ApplicationAlias"] ?? "unknown";
        #endregion

        #region Initialize 方法
        /// <summary>
        /// 初始化ONS事务消息相关对象，一般在Application_Start中调用
        /// </summary>
        public static void Initialize()
        {
            if (_AliyunOnsIsEnabled == "1")
            {
                //初始化服务类列表和服务类标签列表
                InitialProperties();

                //判断生产者的服务类是否存在
                if (ONSProducerServiceList.Count > 0)
                {
                    //输出生产者方法
                    ONSProducerServiceList.ForEach(service => DebugUtil.Debug("生产者的服务类：" + service.GetType().FullName));

                    if (ONSProducerList != null && ONSProducerList.Count > 0)
                    {
                        ONSProducerList.ForEach(producer =>
                        {
                            //启动上游事务生产者
                            producer.start();
                            DebugUtil.Debug("Topic：" + producer.Topic + "，ProducerId（" + producer.Type.ToString() + @"）：" + producer.ProducerId + @"生产者.start()");
                        });
                    }
                }

                //判断消费者的服务类是否存在
                if (ONSConsumerServiceList.Count > 0)
                {
                    //输出消费者方法
                    ONSConsumerServiceList.ForEach(service => DebugUtil.Debug("消费者的服务类：" + service.GetType().FullName));

                    if (ONSConsumerList != null && ONSConsumerList.Count > 0)
                    {
                        ONSConsumerList.ForEach(consumer =>
                        {
                            string tags = GetConsumerServiceTags(consumer.TagList);
                            consumer.subscribe(consumer.Topic, tags);
                            //启动上游事务生产者
                            consumer.start();
                            DebugUtil.Debug("Topic：" + consumer.Topic + "，ConsumerId（" + consumer.Type.ToString() + @"）：" + consumer.ConsumerId + @"消费者.start()，topic:" + consumer.Topic + "，tags:" + tags);
                        });
                    }
                }
            }
        }
        #endregion

        #region Destroy 方法
        /// <summary>
        /// 销毁ONS事务消息相关对象，一般在Application_End中调用
        /// </summary>
        public static void Destroy()
        {
            if (_AliyunOnsIsEnabled == "1")
            {
                if (ONSProducerList != null && ONSProducerList.Count > 0)
                {
                    ONSProducerList.ForEach(producer =>
                    {

                        //关闭上游事务消息生产者
                        producer.shutdown();
                        DebugUtil.Debug("ProducerId（" + producer.Type.ToString() + @"）:" + producer.ProducerId + @"生产者.shutdown()");
                    });
                }

                if (ONSConsumerList != null && ONSConsumerList.Count > 0)
                {
                    ONSConsumerList.ForEach(consumer =>
                    {
                        consumer.shutdown();
                        DebugUtil.Debug("ConsumerId（" + consumer.Type.ToString() + @"）:" + consumer.ConsumerId + @"消费者.shutdown()");
                    });
                }
            }
        }
        #endregion

        /// <summary>
        /// 初始化属性
        /// </summary>
        static void InitialProperties()
        {
            //实例化生产者的服务类列表
            ONSProducerServiceList = new List<object>();
            //实例化消费者的服务类列表
            ONSConsumerServiceList = new List<object>();
            //实例化生产者列表
            ONSProducerList = new List<IONSProducer>();
            //实例化消费者列表
            ONSConsumerList = new List<IONSConsumer>();
            //实例化Executer方法对应的委托实例字典
            ExecuterMethodDictionary = new ConcurrentDictionary<string, MethodInfo>();
            //实例化Checker方法对应的委托实例字典
            CheckerMethodDictionary = new ConcurrentDictionary<string, MethodInfo>();

            //获取当前应用域下的所有程序集
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            //找出所有程序集下带有[ONSConsumer]特性的所有方法，并放入自定义消费者方法列表中
            if (assemblies != null)
            {
                foreach (Assembly assembly in assemblies)
                {
                    Type[] types = assembly.GetTypes();
                    if (types != null)
                    {
                        foreach (Type type in types)
                        {
                            if (type.BaseType != null)
                            {
                                if (type.BaseType.FullName != null)
                                {
                                    
                                    //获取当前应用作为"事务"类型生产者的相关信息
                                    CreateProducer<AbstractTranProducerService<object>>(assembly, type, ONSMessageType.TRAN, (onsProducerFactoryProperty, topic, producerId) =>
                                    {
                                        //实例化ONSLocalTransactionChecker
                                        ONSLocalTransactionChecker checker = new ONSLocalTransactionChecker();
                                        //实例化TransactionProducer
                                        TransactionProducer transactionProducer = ONSFactory.getInstance().createTransactionProducer(onsProducerFactoryProperty, checker);
                                        //实例化代理类ONSTransactionProducer
                                        return new ONSTranProducer(topic, producerId, transactionProducer);
                                    });

                                    /*
                                    //获取当前应用作为"顺序"类型生产者的相关信息
                                    CreateProducer<AbstractOrderProducerService<object>>(assembly, type, ONSMessageType.ORDER, (onsProducerFactoryProperty, topic, producerId) =>
                                    {
                                        //实例化OrderProducer
                                        OrderProducer orderProducer = ONSFactory.getInstance().createOrderProducer(onsProducerFactoryProperty);
                                        //实例化代理类ONSOrderProducer
                                        return new ONSOrderProducer(topic, producerId, orderProducer);
                                    });

                                    //获取当前应用作为"普通"类型生产者的相关信息
                                    CreateProducer<AbstractBaseProducerService<object>>(assembly, type, ONSMessageType.BASE, (onsProducerFactoryProperty, topic, producerId) =>
                                    {
                                        //实例化Producer
                                        Producer baseProducer = ONSFactory.getInstance().createProducer(onsProducerFactoryProperty);
                                        //实例化代理类ONSProducer
                                        return new ONSBaseProducer(topic, producerId, baseProducer);
                                    });
                                    //*/

                                    //获取当前应用作为"事务"类型消费者的相关信息
                                    CreateConsumer<AbstractTranConsumerService<object>>(assembly, type, ONSMessageType.TRAN, (onsConsumerFactoryProperty, topic, consumerId) =>
                                    {
                                        //实例化PushConsumer
                                        PushConsumer pushConsumer = ONSFactory.getInstance().createPushConsumer(onsConsumerFactoryProperty);
                                        //实例化代理类ONSTransactionConsumer
                                        return new ONSTranConsumer(topic, consumerId, pushConsumer);
                                    });

                                    //获取当前应用作为"顺序"类型消费者的相关信息
                                    CreateConsumer<AbstractOrderConsumerService<object>>(assembly, type, ONSMessageType.ORDER, (onsConsumerFactoryProperty, topic, consumerId) =>
                                    {
                                        //实例化OrderConsumer
                                        OrderConsumer orderConsumer = ONSFactory.getInstance().createOrderConsumer(onsConsumerFactoryProperty);
                                        //实例化代理类ONSOrderConsumer
                                        return new ONSOrderConsumer(topic, consumerId, orderConsumer);
                                    });

                                    //获取当前应用作为"普通"类型消费者的相关信息
                                    CreateConsumer<AbstractBaseConsumerService<object>>(assembly, type, ONSMessageType.BASE, (onsConsumerFactoryProperty, topic, consumerId) =>
                                    {
                                        //实例化PushConsumer
                                        PushConsumer pushConsumer = ONSFactory.getInstance().createPushConsumer(onsConsumerFactoryProperty);
                                        //实例化代理类ONSTransactionConsumer
                                        return new ONSBaseConsumer(topic, consumerId, pushConsumer);
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        //*
        internal static void CreateProducer<T>(Assembly assembly, Type type, ONSMessageType messageType, Func<ONSFactoryProperty, string, string, IONSProducer> func)
        {
            if (type.BaseType.FullName.IndexOf(typeof(T).Name) >= 0)
            {
                //添加到生产者服务类实例列表
                //ONSProducerServiceList.Add(type);
                object service = assembly.CreateInstance(type.FullName);
                ONSProducerServiceList.Add(service);
                //获取服务接口
                IAbstractProducerService iservice = (IAbstractProducerService)service;
                //获取枚举对象
                Enum topicTag = iservice.TopicTag;

                if (topicTag != null)
                {
                    string serviceTopic = topicTag.GetType().Name;
                    string topic = (_Environment + "_" + serviceTopic).ToUpper();
                    string producerId = ("PID_" + topic).ToUpper();
                    IONSProducer producer = ONSProducerList.Where(p => (p.Type == messageType.ToString()) && (p.ProducerId == producerId)).FirstOrDefault();
                    if (producer == null)
                    {
                        //实例化ONSFactoryProperty
                        ONSFactoryProperty onsProducerFactoryProperty = new ONSFactoryProperty();
                        onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.AccessKey, _AliyunOnsAccessKey);
                        onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.SecretKey, _AliyunOnsSecretKey);
                        onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.ProducerId, producerId);
                        onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.PublishTopics, topic);
                        //onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.LogPath, "D://log/rocketmq/producer");

                        //获取生产者IONSProducer
                        producer = func(onsProducerFactoryProperty, topic, producerId);

                        //记录生产者初始化信息
                        DebugUtil.Debug("Topic：" + topic + "，ProducerId（" + producer.Type.ToString() + "）：" + producer.ProducerId + "生产者new()");

                        //新增代理类ONSProducer实例到ONSProducerList中
                        ONSProducerList.Add(producer);
                    }
                }
            }
        }
        //*/

        internal static void CreateConsumer<T>(Assembly assembly, Type type, ONSMessageType messageType, Func<ONSFactoryProperty, string, string, IONSConsumer> func)
        {
            if (type.BaseType.FullName.IndexOf(typeof(T).Name) >= 0)
            {
                //添加到消费者服务类实例列表
                //ONSConsumerServiceList.Add(type);
                object service = assembly.CreateInstance(type.FullName);
                ONSConsumerServiceList.Add(service);
                //获取服务接口
                IAbstractConsumerService iservice = (IAbstractConsumerService)service;
                //获取枚举数组对象
                Enum[] topicTagList = iservice.TopicTagList;

                if (topicTagList != null && topicTagList.Length > 0)
                {
                    foreach (Enum topicTag in topicTagList)
                    {
                        string serviceTopic = topicTag.GetType().Name;
                        string serviceTag = topicTag.ToString();


                        string topic = (_Environment + "_" + serviceTopic).ToUpper();
                        string consumerId = ("CID_" + topic + "_" + _ApplicationAlias).ToUpper();

                        IONSConsumer consumer = ONSConsumerList.Where(c => c.Type == messageType.ToString() && c.ConsumerId == consumerId).FirstOrDefault();
                        if (consumer == null)
                        {
                            //实例化ONSFactoryProperty
                            ONSFactoryProperty onsConsumerFactoryProperty = new ONSFactoryProperty();
                            onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.AccessKey, _AliyunOnsAccessKey);
                            onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.SecretKey, _AliyunOnsSecretKey);
                            onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.ConsumerId, consumerId);
                            //onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.PublishTopics, _ONSTopic);
                            //onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.LogPath, "D://log/rocketmq/consumer");

                            //获取消费者IONSConsumer
                            consumer = func(onsConsumerFactoryProperty, topic, consumerId);

                            //记录消费者初始化信息
                            DebugUtil.Debug("Topic：" + topic + "，ConsumerId（" + consumer.Type + "）：" + consumer.ConsumerId + "消费者.new()");

                            //新增代理类ONSConsumer实例到ONSConsumerList中
                            ONSConsumerList.Add(consumer);
                        }

                        if (!consumer.TagList.Contains(serviceTag))
                        {
                            consumer.TagList.Add(serviceTag);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取字符串格式的标签列表
        /// </summary>
        /// <returns>返回字符串格式的标签列表</returns>
        static string GetConsumerServiceTags(List<string> tagList)
        {
            string consumerTags = "";
            for (int i = 0; i < tagList.Count; i++)
            {
                if (i < tagList.Count - 1)
                {
                    consumerTags += tagList[i] + "||";
                }
                else
                {
                    consumerTags += tagList[i];
                }
            }
            return consumerTags;
        }
    }
}