using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Reflection;
using System.Collections.Concurrent;
using System.Configuration;
using System.Text;
using System.Threading;
using System.IO;
using ons;
using Redis.Framework;
using Newtonsoft.Json;
using Liinji.Common;
using Nest.Framework;

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
        public static List<object> ONSConsumerServiceList { get; private set; }

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
        static string _Environment = ConfigurationManager.AppSettings["Environment"] ?? "s";
        //获取当前应用的别名
        static string _ApplicationAlias = ConfigurationManager.AppSettings["ApplicationAlias"] ?? "unknown";
        //获取生产者sdk日志路径
        static string _AliyunOnsProducerLogPath = ConfigurationManager.AppSettings["AliyunOnsProducerLogPath"] ?? "";
        //获取消费者sdk日志路径
        static string _AliyunOnsConsumerLogPath = ConfigurationManager.AppSettings["AliyunOnsConsumerLogPath"] ?? "";
        #endregion

        #region Initialize 方法
        /// <summary>
        /// 初始化ONS事务消息相关对象，一般在Application_Start中调用
        /// </summary>
        public static void Initialize()
        {
            if (_AliyunOnsIsEnabled == "1")
            {
                try
                {
                    //初始化服务类列表和服务类标签列表
                    InitializeProperties();

                    //判断生产者的服务类是否存在
                    if (ONSProducerServiceList.Count > 0)
                    {
                        //输出生产服务类类名
                        ONSProducerServiceList.ForEach(service => DebugUtil.Debug("生产者的服务类：" + service.GetType().FullName));

                        //判断生产者实例列表是否为空
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
                        //输出消费服务类类名
                        ONSConsumerServiceList.ForEach(service => DebugUtil.Debug("消费者的服务类：" + service.GetType().FullName));

                        //判断消费者实例列表是否为空
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

                    //延时若干毫秒
                    Thread.Sleep(1000);
                    DebugUtil.Debug(_Environment + "." + _ApplicationAlias + ".ONSHelper.Initialize最后延迟1000毫秒");
                }
                catch (Exception e)
                {
                    //记录本地错误日志
                    DebugUtil.Debug(_Environment + "." + _ApplicationAlias + ".ONSHelper.Initialize出错：" + e.ToString());

                    //记录FATAL日志，发送FATAL目前出错
                    //SaveLog(LogTypeEnum.FATAL, "ONSHelper", "Initialize", _Environment + "." + _ApplicationAlias + ".ONSHelper.Initialize出错：" + e.ToString());

                    //发送邮件
                    SendDebugMail(_Environment + "." + _ApplicationAlias + ".ONSHelper.Initialize出错", "：" + e.ToString());
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
                try
                {
                    if (ONSProducerList != null && ONSProducerList.Count > 0)
                    {
                        ONSProducerList.ForEach(producer =>
                        {

                            //关闭上游消息生产者
                            DebugUtil.Debug("ProducerId（" + producer.Type.ToString() + @"）:" + producer.ProducerId + @"生产者.准备shutdown()");
                            producer.shutdown();
                            DebugUtil.Debug("ProducerId（" + producer.Type.ToString() + @"）:" + producer.ProducerId + @"生产者.shutdown()");
                        });
                    }

                    if (ONSConsumerList != null && ONSConsumerList.Count > 0)
                    {
                        ONSConsumerList.ForEach(consumer =>
                        {
                            //关闭下游消息消费者
                            DebugUtil.Debug("ConsumerId（" + consumer.Type.ToString() + @"）:" + consumer.ConsumerId + @"消费者.准备shutdown()");
                            consumer.shutdown();
                            DebugUtil.Debug("ConsumerId（" + consumer.Type.ToString() + @"）:" + consumer.ConsumerId + @"消费者.shutdown()");
                        });
                    }

                    //延时若干毫秒
                    Thread.Sleep(1000);
                    DebugUtil.Debug(_Environment + "." + _ApplicationAlias + ".ONSHelper.Destroy最后延迟1000毫秒");
                }
                catch (Exception e)
                {
                    //记录本地错误日志
                    DebugUtil.Debug(_Environment + "." + _ApplicationAlias + ".ONSHelper.Destroy出错：" + e.ToString());

                    //记录FATAL日志，发送FATAL目前出错
                    //SaveLog(LogTypeEnum.FATAL, "ONSHelper", "Destroy", _Environment + "." + _ApplicationAlias + ".ONSHelper.Destroy出错：" + e.ToString());

                    //发送邮件
                    SendDebugMail(_Environment + "." + _ApplicationAlias + ".ONSHelper.Destroy出错", "：" + e.ToString());
                }
            }
        }
        #endregion

        #region 初始化属性
        /// <summary>
        /// 初始化属性
        /// </summary>
        static void InitializeProperties()
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


            

            //*
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

                                    ////获取当前应用作为"顺序"类型生产者的相关信息
                                    //CreateProducer<AbstractOrderProducerService<object>>(assembly, type, ONSMessageType.ORDER, (onsProducerFactoryProperty, topic, producerId) =>
                                    //{
                                    //    //实例化OrderProducer
                                    //    OrderProducer orderProducer = ONSFactory.getInstance().createOrderProducer(onsProducerFactoryProperty);
                                    //    //实例化代理类ONSOrderProducer
                                    //    return new ONSOrderProducer(topic, producerId, orderProducer);
                                    //});

                                    ////获取当前应用作为"普通"类型生产者的相关信息
                                    //CreateProducer<AbstractBaseProducerService<object>>(assembly, type, ONSMessageType.BASE, (onsProducerFactoryProperty, topic, producerId) =>
                                    //{
                                    //    //实例化Producer
                                    //    Producer baseProducer = ONSFactory.getInstance().createProducer(onsProducerFactoryProperty);
                                    //    //实例化代理类ONSProducer
                                    //    return new ONSBaseProducer(topic, producerId, baseProducer);
                                    //});

                                    //获取当前应用作为"事务"类型消费者的相关信息
                                    CreateConsumer<AbstractTranConsumerService<object>>(assembly, type, ONSMessageType.TRAN, (onsConsumerFactoryProperty, topic, consumerId, classType) =>
                                    {
                                        //实例化PushConsumer
                                        PushConsumer pushConsumer = ONSFactory.getInstance().createPushConsumer(onsConsumerFactoryProperty);
                                        //实例化代理类ONSTransactionConsumer
                                        return new ONSTranConsumer(topic, consumerId, pushConsumer, classType);
                                    });

                                    //获取当前应用作为"顺序"类型消费者的相关信息
                                    CreateConsumer<AbstractOrderConsumerService<object>>(assembly, type, ONSMessageType.ORDER, (onsConsumerFactoryProperty, topic, consumerId, classType) =>
                                    {
                                        //实例化OrderConsumer
                                        OrderConsumer orderConsumer = ONSFactory.getInstance().createOrderConsumer(onsConsumerFactoryProperty);
                                        //实例化代理类ONSOrderConsumer
                                        return new ONSOrderConsumer(topic, consumerId, orderConsumer, classType);
                                    });

                                    //获取当前应用作为"普通"类型消费者的相关信息
                                    CreateConsumer<AbstractBaseConsumerService<object>>(assembly, type, ONSMessageType.BASE, (onsConsumerFactoryProperty, topic, consumerId, classType) =>
                                    {
                                        //实例化PushConsumer
                                        PushConsumer pushConsumer = ONSFactory.getInstance().createPushConsumer(onsConsumerFactoryProperty);
                                        //实例化代理类ONSTransactionConsumer
                                        return new ONSBaseConsumer(topic, consumerId, pushConsumer, classType);
                                    });
                                }
                            }
                        }
                    }
                }
            }
            //*/
        }
        #endregion

        #region 创建生产者实例
        /// <summary>
        /// 创建生产者实例
        /// </summary>
        /// <typeparam name="T">生产者服务基类类型</typeparam>
        /// <param name="assembly">生产者服务类所在程序集</param>
        /// <param name="type">生产者服务类的类型</param>
        /// <param name="messageType">消息类型BASE，ORDER，TRAN</param>
        /// <param name="func">生成生产者实例的委托代码</param>
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

                        if (_AliyunOnsProducerLogPath != "")
                        {
                            if (Directory.Exists(_AliyunOnsProducerLogPath))
                            {
                                onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.LogPath, _AliyunOnsProducerLogPath);
                            }
                        }

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
        #endregion

        #region 生成消费者实例
        /// <summary>
        /// 生成消费者实例
        /// </summary>
        /// <typeparam name="T">消费者服务基类类型</typeparam>
        /// <param name="assembly">消费者服务类所在程序集</param>
        /// <param name="type">消费者服务类的类型</param>
        /// <param name="messageType">消息类型BASE，ORDER，TRAN</param>
        /// <param name="func">生成消费者实例的委托代码</param>
        internal static void CreateConsumer<T>(Assembly assembly, Type type, ONSMessageType messageType, Func<ONSFactoryProperty, string, string, Type, IONSConsumer> func)
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
                        string className = type.Name;

                        string topic = (_Environment + "_" + serviceTopic).ToUpper();
                        string consumerId = ("CID_" + topic + "_" + _ApplicationAlias + "_" + className).ToUpper();

                        DebugUtil.Debug("consumerId:" + consumerId);

                        IONSConsumer consumer = ONSConsumerList.Where(c => c.Type == messageType.ToString() && c.ConsumerId == consumerId).FirstOrDefault();
                        if (consumer == null)
                        {
                            //实例化ONSFactoryProperty
                            ONSFactoryProperty onsConsumerFactoryProperty = new ONSFactoryProperty();
                            onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.AccessKey, _AliyunOnsAccessKey);
                            onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.SecretKey, _AliyunOnsSecretKey);
                            onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.ConsumerId, consumerId);
                            //onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.PublishTopics, _ONSTopic);

                            if (_AliyunOnsConsumerLogPath != "")
                            {
                                if (Directory.Exists(_AliyunOnsConsumerLogPath))
                                {
                                    onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.LogPath, _AliyunOnsConsumerLogPath);
                                }
                            }

                            //获取消费者IONSConsumer
                            consumer = func(onsConsumerFactoryProperty, topic, consumerId, type);

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
        #endregion

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

        /// <summary>
        /// 记录日志（建议在有上下文的环境下使用，否则可能会报错）
        /// </summary>
        /// <param name="logType">日志类型TRACE，INFO，ERROR，FATAL</param>
        /// <param name="className">哪个类中出错</param>
        /// <param name="methodName">哪个方法中出错</param>
        /// <param name="content">日志内容</param>
        public static void SaveLog(LogTypeEnum logType, string className, string methodName, string content)
        {
            try
            {
                LogParameter logParameter = new LogParameter();
                logParameter.LogType = logType;
                logParameter.ClassName = className;
                logParameter.MethodName = methodName;
                logParameter.LogMessage = content;
                NestLogHelper.WriteLog(logParameter);
            }
            catch (Exception esError)
            {
                //发送es日志异常
                DebugUtil.Debug(content + "，尝试发送es的FATAL日志时发生异常：" + esError.ToString());
            }
        }

        /// <summary>
        /// 发送DEBUG邮件，一般在发生严重FATAL错误时，需要发送邮件通知该网站相关人员
        /// </summary>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">邮件内容</param>
        public static void SendDebugMail(string subject, string body)
        {
            try
            {
                string resultMessage = "";
                MailUtil.SendDebugMail(subject, body, out resultMessage);
            }
            catch (Exception mailError)
            {
                //发送邮件错误
                DebugUtil.Debug(subject +"，"+ body + "，尝试发送邮件时发生异常：" + mailError.ToString());
            }
        }
    }
}