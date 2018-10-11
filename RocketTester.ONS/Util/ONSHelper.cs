using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Reflection;
using System.Collections.Concurrent;
using System.Configuration;
using ons;
using RocketTester.ONS.Model;
using RocketTester.ONS.Service;
using Redis.Framework;
using Newtonsoft.Json;

namespace RocketTester.ONS.Util
{
    /// <summary>
    /// 事务消息帮助类，事务未提交状态，第一次回查（调用Checker.execute方法）在0~5秒后，之后每5秒会再次回查。消费未提交的话，历经16次4小时46分钟。
    /// </summary>
    public class ONSHelper
    {
        #region 属性

        /// <summary>
        /// 上游生产者服务类列表
        /// </summary>
        public static List<Type> ONSTransactionProducerServiceList { get; set; }

        /// <summary>
        /// 下游消费者服务类列表
        /// </summary>
        public static List<Type> ONSConsumerServiceList { get; set; }

        /// <summary>
        /// 上游生产者标签列表
        /// </summary>
        public static List<string> ONSTransactionProducerServiceTagList { get; set; }

        /// <summary>
        /// 下游消费者标签列表
        /// </summary>
        public static List<string> ONSConsumerServiceTagList { get; set; }

        /// <summary>
        /// executer方法实例字典
        /// </summary>
        public static ConcurrentDictionary<string, MethodInfo> ExecuterMethodDictionary { get; set; }

        /// <summary>
        /// checker方法实例字典
        /// </summary>
        public static ConcurrentDictionary<string, MethodInfo> CheckerMethodDictionary { get; set; }
        #endregion

        #region 字段
        //redis地址
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        //启用redis第N个数据库
        static int _ONSRedisDBNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisDBNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["ONSRedisDBNumber"]);
        //获取事务消息主题
        static string _ONSTopic = ConfigurationManager.AppSettings["ONSTopic"] ?? "";
        //获取事务消息的上游生产者唯一标识
        static string _ONSProducerId = ConfigurationManager.AppSettings["ONSProducerId"] ?? "";
        //获取事务消息的下游消费者唯一标识
        static string _ONSConsumerId = ConfigurationManager.AppSettings["ONSConsumerId"] ?? "";
        //获取RAM控制台消息队列账号的AccessKey
        static string _ONSAccessKey = ConfigurationManager.AppSettings["ONSAccessKey"] ?? "";
        //获取RAM控制台消息队列账号的SecretKey
        static string _ONSSecretKey = ConfigurationManager.AppSettings["ONSSecretKey"] ?? "";
        //获取当前环境，p代表生产环境production，s代表测试环境staging，d代表开发环境development
        static string _Environment = ConfigurationManager.AppSettings["Environment"] ?? "p";

        static object _transactionProducerLockHelper = new object();
        static TransactionProducer _transactionProducer;

        static object _onsProducerFactoryPropertyLockHelper = new object();
        static ONSFactoryProperty _onsProducerFactoryProperty;

        static object _pushConsumerLockHelper = new object();
        static PushConsumer _pushConsumer;

        static object _onsConsumerFactoryPropertyLockHelper = new object();
        static ONSFactoryProperty _onsConsumerFactoryProperty;

        #endregion

        #region ONSProducerFactoryProperty 属性
        /// <summary>
        /// 消息的上游生产者所需的ONSFactoryProperty属性
        /// </summary>
        static ONSFactoryProperty ONSProducerFactoryProperty
        {
            get
            {
                return GetONSProducerFactoryPropertyInstance();
            }
        }

        /// <summary>
        /// 获取消息的上游生产者所需的ONSFactoryProperty实例
        /// </summary>
        /// <returns></returns>
        static ONSFactoryProperty GetONSProducerFactoryPropertyInstance()
        {
            if (_onsProducerFactoryProperty == null)
            {
                lock (_onsProducerFactoryPropertyLockHelper)
                {
                    if (_onsProducerFactoryProperty == null)
                    {
                        _onsProducerFactoryProperty = new ONSFactoryProperty();
                        _onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.AccessKey, _ONSAccessKey);
                        _onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.SecretKey, _ONSSecretKey);
                        _onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.ProducerId, _ONSProducerId);
                        //_onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.ConsumerId, _ONSConsumerId);
                        _onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.PublishTopics, _ONSTopic);
                        _onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.LogPath, "D://log/rocketmq/producer");
                    }
                }
            }

            return _onsProducerFactoryProperty;
        }
        #endregion

        #region TransactionProducer 属性
        /// <summary>
        /// 消息的上游生产者
        /// </summary>
        public static TransactionProducer TransactionProducer
        {
            get
            {
                return GetTransactionProducerInstance();
            }
        }

        /// <summary>
        /// 获取消息的上游生产者实例
        /// </summary>
        /// <returns></returns>
        static TransactionProducer GetTransactionProducerInstance()
        {
            if (_transactionProducer == null)
            {
                lock (_transactionProducerLockHelper)
                {
                    if (_transactionProducer == null)
                    {
                        ONSLocalTransactionChecker checker = new ONSLocalTransactionChecker();
                        _transactionProducer = ONSFactory.getInstance().createTransactionProducer(ONSProducerFactoryProperty, checker);
                    }
                }
            }

            return _transactionProducer;
        }
        #endregion

        #region ONSConsumerFactoryProperty 属性
        /// <summary>
        /// 下游消费者所需的ONSFactoryProperty属性
        /// </summary>
        static ONSFactoryProperty ONSConsumerFactoryProperty
        {
            get
            {
                return GetONSConsumerFactoryPropertyInstance();
            }
        }

        /// <summary>
        /// 获取下游消费者所需的ONSFactoryProperty实例
        /// </summary>
        /// <returns></returns>
        static ONSFactoryProperty GetONSConsumerFactoryPropertyInstance()
        {
            if (_onsConsumerFactoryProperty == null)
            {
                lock (_onsConsumerFactoryPropertyLockHelper)
                {
                    if (_onsConsumerFactoryProperty == null)
                    {
                        _onsConsumerFactoryProperty = new ONSFactoryProperty();
                        _onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.AccessKey, _ONSAccessKey);
                        _onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.SecretKey, _ONSSecretKey);
                        //_onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.ProducerId, _ONSProducerId);
                        _onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.ConsumerId, _ONSConsumerId);
                        _onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.PublishTopics, _ONSTopic);
                        _onsConsumerFactoryProperty.setFactoryProperty(ONSFactoryProperty.LogPath, "D://log/rocketmq/consumer");
                    }
                }
            }

            return _onsConsumerFactoryProperty;
        }
        #endregion

        #region PushConsumer 属性
        /// <summary>
        /// 消息的下游消费者
        /// </summary>
        public static PushConsumer PushConsumer
        {
            get
            {
                return GetPushConsumerInstance();
            }
        }

        /// <summary>
        /// 获取消息的下游消费者实例
        /// </summary>
        /// <returns>PushConsumer实例</returns>
        static PushConsumer GetPushConsumerInstance()
        {
            if (_pushConsumer == null)
            {
                lock (_pushConsumerLockHelper)
                {
                    if (_pushConsumer == null)
                    {
                        _pushConsumer = ONSFactory.getInstance().createPushConsumer(ONSConsumerFactoryProperty);
                    }
                }
            }

            return _pushConsumer;
        }
        #endregion

        /*
        #region Transact 方法
        /// <summary>
        /// 执行委托实例，如果逻辑上成功，则由上游事务消息生产者发送消息到消息中心
        /// </summary>
        /// <param name="executerFunc">executer委托实例</param>
        /// <param name="executerFuncModel">executer委托实例所需的参数</param>
        /// <returns></returns>
        public static ONSTransactionResult Transact(Func<string, ONSTransactionResult> executerFunc, string executerFuncModel)
        {
            return Transact(executerFunc, executerFuncModel, executerFunc, executerFuncModel);
        }

        /// <summary>
        /// 执行委托实例，如果逻辑上成功，则由上游事务消息生产者发送消息到消息中心
        /// </summary>
        /// <param name="executerFunc">executer委托实例</param>
        /// <param name="executerFuncModel">executer委托实例所需的参数</param>
        /// <param name="checkerFunc">checker委托实例</param>
        /// <param name="checkerFuncModel">checker委托实例所需的参数</param>
        /// <returns></returns>
        public static ONSTransactionResult Transact(Func<string, ONSTransactionResult> executerFunc, string executerFuncModel, Func<string, ONSTransactionResult> checkerFunc, string checkerFuncModel)
        {
            MethodInfo checkerFuncMethodInfo = checkerFunc.Method;
            MethodInfo executerFuncMethodInfo = executerFunc.Method;
            ONSProducerAttribute oneMethodAttribute = executerFuncMethodInfo.GetCustomAttribute<ONSProducerAttribute>();

            //body不能为空，否则要报错，Func<string,TransactionResult>对应方法中，lambda什么的错误，实际根本没错，就是Message实体的body为空
            Message message = new Message(_Environment + "_" + oneMethodAttribute.Topic.ToString().ToLower(), oneMethodAttribute.Tag.ToString(), "no content");
            string key = _ONSTopic + "_" + oneMethodAttribute.Tag.ToString() + "_" + Guid.NewGuid().ToString();

            //设置key作为自定义的消息唯一标识，不能用ONS消息自带的MsgId作为消息的唯一标识，因为它不保证一定不出现重复。
            message.setKey(key);

            LogHelper.Log("topic " + oneMethodAttribute.Topic);
            LogHelper.Log("tag " + oneMethodAttribute.Tag);

            string executerFuncName = executerFuncMethodInfo.ReflectedType.FullName + "." + executerFuncMethodInfo.Name;
            string checkerFuncName = checkerFuncMethodInfo.ReflectedType.FullName + "." + checkerFuncMethodInfo.Name;

            //将委托实例和委托实例的参数都存到消息的属性中去。
            message.putUserProperties("executerFuncModel", executerFuncModel);
            message.putUserProperties("executerFunc", executerFuncName);
            message.putUserProperties("checkerFuncModel", checkerFuncModel);
            message.putUserProperties("checkerFunc", checkerFuncName);

            //委托实例字典中不存在的话，则试图新增到字典中去
            if (!ExecuterFuncDictionary.ContainsKey(executerFuncName))
            {
                ExecuterFuncDictionary.TryAdd(executerFuncName, executerFunc);
            }
            if (!CheckerFuncDictionary.ContainsKey(checkerFuncName))
            {
                CheckerFuncDictionary.TryAdd(checkerFuncName, checkerFunc);
            }

            //实例化LocalTransactionExecuter对象
            ONSLocalTransactionExecuter executer = new ONSLocalTransactionExecuter();

            LogHelper.Log("get ready to send message...");

            //生成半消息，并调用LocalTransactionExecuter对象的execute方法，它内部会执行委托实例（同时会将执行后的TransactionResult以Message的key为redis的key存入redis中），根据执行结果再决定是否要将消息状态设置为rollback或commit
            SendResultONS sendResultONS = TransactionProducer.send(message, executer);

            LogHelper.Log("key " + key);

            //实例化redis工具
            RedisTool RT = new RedisTool(_ONSRedisDBNumber, _RedisExchangeHosts);
            //在redis中按消息的key获取其值（即委托实例执行后返回的一个TransactionResult对象，并做json序列化）
            string result = RT.StringGet(key) ?? "";

            LogHelper.Log("result " + result);
            LogHelper.Log("");

            //反序列化获取到一个TransactionResult对象
            return JsonConvert.DeserializeObject<ONSTransactionResult>(result);
        }
        #endregion
        //*/

        #region Initialize 方法
        /// <summary>
        /// 初始化ONS事务消息相关对象，一般在Application_Start中调用
        /// </summary>
        public static void Initialize()
        {
            //实例化生产者的服务类列表
            ONSTransactionProducerServiceList = new List<Type>();
            //实例化消费者的服务类列表
            ONSConsumerServiceList = new List<Type>();
            //实例化生产者的服务类标签列表
            ONSTransactionProducerServiceTagList = new List<string>();
            //实例化消费者的服务类标签列表
            ONSConsumerServiceTagList = new List<string>();

            //初始化服务类列表和服务类标签列表
            InitialServiceListAndServiceTagList();
            //获取字符串格式的消费者方法特性的标签列表
            string consumerServiceTagString = GetConsumerServiceTagString();
            LogHelper.Log("ONSConsumerServiceList.Count " + ONSConsumerServiceList.Count);

            //实例化Executer方法对应的委托实例字典
            ExecuterMethodDictionary = new ConcurrentDictionary<string, MethodInfo>();
            //实例化Checker方法对应的委托实例字典
            CheckerMethodDictionary = new ConcurrentDictionary<string, MethodInfo>();


            //判断生产者的服务类是否存在
            if (ONSTransactionProducerServiceList.Count > 0)
            {
                //输出生产者方法
                ONSTransactionProducerServiceList.ForEach(type => LogHelper.Log("生产者的服务类：" + type.FullName));
                //启动上游事务生产者
                TransactionProducer.start();
                LogHelper.Log("ONSHelper.TransactionProducer.start");
            }

            //判断消费者的服务类是否存在
            if (ONSConsumerServiceList.Count > 0)
            {
                //输出消费者方法
                ONSConsumerServiceList.ForEach(type => LogHelper.Log("消费者的服务类：" + type.FullName));
                //下游事务消费者订阅上游事务消息生产的消息
                PushConsumer.subscribe(_ONSTopic, consumerServiceTagString, new ONSMessageListener());
                LogHelper.Log("ONSHelper.PushConsumer.subscribe " + consumerServiceTagString);
                //启动下游事务消费者
                PushConsumer.start();
                LogHelper.Log("ONSHelper.PushConsumer.start");
            }
        }
        #endregion

        #region Destroy 方法
        /// <summary>
        /// 销毁ONS事务消息相关对象，一般在Application_End中调用
        /// </summary>
        public static void Destroy()
        {
            //这里千万不能用ONSHelper.TransactionProducer判断是否不为null，这样会导致被实例化
            if (ONSHelper._transactionProducer != null)
            {
                //关闭上游事务消息生产者
                TransactionProducer.shutdown();
                LogHelper.Log("ONSHelper.TransactionProducer.shutdown");
            }

            //这里千万不能用ONSHelper.PushConsumer判断是否不为null，这样会导致被实例化
            if (ONSHelper._pushConsumer != null)
            {
                //关闭下游事务消息消费者
                PushConsumer.shutdown();
                LogHelper.Log("ONSHelper.PushConsumer.shutdown");
            }
        }
        #endregion

        /// <summary>
        /// 初始化消费者的方法和标签列表
        /// </summary>
        static void InitialServiceListAndServiceTagList()
        {
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
                                //LogHelper.Log("" + type.FullName);

                                
                                //获取当前应用作为生产者的相关信息
                                if (type.BaseType.FullName != null)
                                {
                                    if (type.BaseType.FullName.IndexOf("BaseTransactionProducerService") >= 0)
                                    {
                                        ONSTransactionProducerServiceList.Add(type);
                                        object service = assembly.CreateInstance(type.FullName);
                                        string serviceTag = type.GetProperty("Tag").GetValue(service).ToString();
                                        //判断是否存在tag，不存在则新增
                                        if (!ONSTransactionProducerServiceTagList.Contains(serviceTag))
                                        {
                                            ONSTransactionProducerServiceTagList.Add(serviceTag);
                                        }
                                    }
                                }

                                //*
                                //获取当前应用作为消费者的相关信息
                                if (type.BaseType.FullName != null)
                                {
                                    if (type.BaseType.FullName.IndexOf("BaseConsumerService") >= 0)
                                    {
                                        ONSConsumerServiceList.Add(type);
                                        object service = assembly.CreateInstance(type.FullName);
                                        string serviceTag = type.GetProperty("Tag").GetValue(service).ToString();
                                        //判断是否存在tag，不存在则新增
                                        if (!ONSConsumerServiceTagList.Contains(serviceTag))
                                        {
                                            ONSConsumerServiceTagList.Add(serviceTag);
                                        }//*/
                                    }
                                }
                                //*/
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取字符串格式的标签列表
        /// </summary>
        /// <returns>返回字符串格式的标签列表</returns>
        static string GetConsumerServiceTagString()
        {
            string consumerTags = "";
            for (int i = 0; i < ONSConsumerServiceTagList.Count; i++)
            {
                if (i < ONSConsumerServiceTagList.Count - 1)
                {
                    consumerTags += ONSConsumerServiceTagList[i] + "||";
                }
                else
                {
                    consumerTags += ONSConsumerServiceTagList[i];
                }
            }
            return consumerTags;
        }
    }
}