using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Reflection;
using System.Collections.Concurrent;
using System.Configuration;
using ons;
using RocketTester.ONS.Model;
using Redis.Framework;
using Newtonsoft.Json;

namespace RocketTester.ONS.Util
{
    public class ONSHelper
    {
        #region 属性
        /// <summary>
        /// 下游消费者方法列表
        /// </summary>
        public static List<MethodInfo> ONSConsumerMethodInfoList { get; set; }

        /// <summary>
        /// executer委托实例字典
        /// </summary>
        public static ConcurrentDictionary<string, Func<string, ONSTransactionResult>> ExecuterFuncDictionary { get; set; }

        /// <summary>
        /// checker委托实例字典
        /// </summary>
        public static ConcurrentDictionary<string, Func<string, ONSTransactionResult>> CheckerFuncDictionary { get; set; }
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

        private static object _transactionProducerLockHelper = new object();
        private static TransactionProducer _transactionProducer;

        private static object _onsProducerFactoryPropertyLockHelper = new object();
        private static ONSFactoryProperty _onsProducerFactoryProperty;

        private static object _pushConsumerLockHelper = new object();
        private static PushConsumer _pushConsumer;

        private static object _onsConsumerFactoryPropertyLockHelper = new object();
        private static ONSFactoryProperty _onsConsumerFactoryProperty;

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
            Message message = new Message(oneMethodAttribute.Topic.ToString().ToLower(), oneMethodAttribute.Tag.ToString(), "no content");
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

        #region Initialize 方法
        /// <summary>
        /// 初始化ONS事务消息相关对象，一般在Application_Start中调用
        /// </summary>
        public static void Initialize()
        {
            //初始化自定义消费者方法列表
            ONSConsumerMethodInfoList = new List<MethodInfo>();
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
                            MethodInfo[] methodInfos = type.GetMethods();
                            if (methodInfos != null)
                            {
                                foreach (MethodInfo methodInfo in methodInfos)
                                {
                                    bool hasONSConsumerAttribute = false;
                                    IEnumerable<ONSConsumerAttribute> oneConsumerAttributes = methodInfo.GetCustomAttributes<ONSConsumerAttribute>();
                                    if (oneConsumerAttributes != null)
                                    {
                                        foreach (ONSConsumerAttribute onsConsumerAttribute in oneConsumerAttributes)
                                        {
                                            hasONSConsumerAttribute = true;
                                        }
                                    }

                                    if (hasONSConsumerAttribute)
                                    {
                                        ONSConsumerMethodInfoList.Add(methodInfo);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            LogHelper.Log("ONSConsumerMethodInfoList.Count " + ONSConsumerMethodInfoList.Count);

            //初始化Executer方法对应的委托实例字典
            ExecuterFuncDictionary = new ConcurrentDictionary<string, Func<string, ONSTransactionResult>>();
            //初始化Checker方法对应的委托实例字典
            CheckerFuncDictionary = new ConcurrentDictionary<string, Func<string, ONSTransactionResult>>();
            //启动上游事务生产者
            ONSHelper.TransactionProducer.start();
            //下游事务消费者订阅上游事务消息生产的消息
            ONSHelper.PushConsumer.subscribe(_ONSTopic, "*", new ONSMessageListener());
            //启动下游事务消费者
            ONSHelper.PushConsumer.start();
        }
        #endregion

        #region Destroy 方法
        /// <summary>
        /// 销毁ONS事务消息相关对象，一般在Application_End中调用
        /// </summary>
        public static void Destroy()
        {
            if (ONSHelper.TransactionProducer != null)
            {
                //关闭上游事务消息生产者
                ONSHelper.TransactionProducer.shutdown();
            }
            if (ONSHelper.PushConsumer != null)
            {
                //关闭下游事务消息消费者
                ONSHelper.PushConsumer.shutdown();
            }
        }
        #endregion
    }
}