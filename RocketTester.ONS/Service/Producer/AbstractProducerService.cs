using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Reflection;
using System.Web;
using System.Runtime.Remoting.Messaging;
using Nest.Framework;
using Newtonsoft.Json;
using ons;
using Liinji.Common;

namespace RocketTester.ONS
{
    public abstract class AbstractProducerService<T> : IAbstractProducerService
    {
        //获取当前环境，p代表生产环境production，u代表预生产环境uat，s代表测试环境staging，d代表开发环境development（理论上不存在开发环境）
        protected static string _Environment = ConfigurationManager.AppSettings["Environment"] ?? "s";
        //应用别名
        protected static string _ApplicationAlias = ConfigurationManager.AppSettings["ApplicationAlias"] ?? "unknown";
        //获取是否允许发送消息，此开关用于初次上线正式时，由于要观察一段时间，因此AliyunOnsIsEnabled是"1"，但是AliyunOnsIsAllowedToSend是"0"
        protected static string _AliyunOnsIsAllowedToSend = ConfigurationManager.AppSettings["AliyunOnsIsAllowedToSend"] ?? "1";
        //获取RAM控制台消息队列账号的AccessKey
        static string _AliyunOnsAccessKey = ConfigurationManager.AppSettings["AliyunOnsAccessKey"] ?? "";
        //获取RAM控制台消息队列账号的SecretKey
        static string _AliyunOnsSecretKey = ConfigurationManager.AppSettings["AliyunOnsSecretKey"] ?? "";
        //锁的帮助实例
        static object _lockHelper = new object();

        /// <summary>
        /// 消息主题
        /// </summary>
        protected string Topic { get; private set; }

        /// <summary>
        /// 消息标题（如果ProducerService最终以单实例调用，此属性则不能抽象成属性）
        /// </summary>
        protected string Tag { get; private set; }

        /// <summary>
        /// 生产者pid
        /// </summary>
        protected string Pid { get; private set; }

        /// <summary>
        /// 主题标签，用{Topic}.{Tag}格式的枚举实现（如果ProducerService最终以单实例调用，此属性则不能抽象成属性）
        /// </summary>
        public Enum TopicTag { get; private set; }

        /// <summary>
        /// 消息类别，分为BASE，ORDER，TRAN
        /// </summary>
        protected ONSMessageType MessageType { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="messageType">消息类别，分为BASE，ORDER，TRAN</param>
        /// <param name="topicTag">主题标签，用{Topic}.{Tag}格式的枚举实现</param>
        public AbstractProducerService(ONSMessageType messageType, Enum topicTag)
        {
            this.MessageType = messageType;
            this.TopicTag = topicTag;
            this.Topic = (_Environment + "_" + TopicTag.GetType().Name).ToUpper();
            this.Tag = TopicTag.ToString();
            this.Pid = ("PID_" + this.Topic).ToUpper();
        }

        /// <summary>
        /// 获取消息的key
        /// </summary>
        /// <returns></returns>
        protected string CreateMessageKey()
        {
            string key = _Environment + "_" + _ApplicationAlias + ":" + this.Topic + ":" + this.Tag + ":" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ":" + Guid.NewGuid().ToString();
            return key;
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        /// <param name="key">消息的key</param>
        /// <param name="message">消息的正文内容，即model序列化后的内容</param>
        /// <param name="shardingKey">顺序消息要用的shardingKey，其他消息留空字符串</param>
        /// <param name="transactionStatus">事务消息的返回类型，其他消息留空字符串</param>
        /// <param name="failureReason">失败原因，通常有错误时会写入错误原因</param>
        /// <param name="accomplishment">是否已经完成消息</param>
        /// <param name="producedTimes">上游执行次数</param>
        /// <param name="serviceResult">上游执行结果</param>
        protected void LogData(string key, string message, string shardingKey, string transactionStatus, string failureReason, bool accomplishment, int producedTimes, bool serviceResult)
        {
            try
            {
                ProducerData producerData = new ProducerData(this.GetRequestTraceId());
                producerData.Accomplishment = accomplishment;
                producerData.ApplicationAlias = _ApplicationAlias;
                producerData.Topic = this.Topic;
                producerData.Tag = this.Tag;
                producerData.ProducerId = this.Pid;
                producerData.Key = key;
                producerData.Type = this.MessageType.ToString();
                producerData.Message = Base64Util.Decode(message);
                producerData.TransactionType = "";
                producerData.Method = this.GetType().Name;
                producerData.ServiceResult = serviceResult;
                producerData.TransactionStatus = transactionStatus;
                producerData.FailureReason = failureReason;
                producerData.ProducedTimes = producedTimes;
                producerData.ShardingKey = shardingKey;
                producerData.ServerIp = ONSHelper.GetServerIp();
                NestDataHelper.WriteData(producerData);
            }
            catch (Exception e)
            {
                //如果es发送异常，以后可以发送邮件，目前暂时不处理
            }
        }

        /// <summary>
        /// ProcessCore抽象方法，主要用于派生类重写它逻辑，业务方法，需要开发人员自己实现里面的业务逻辑。
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>事务执行结果</returns>
        protected abstract bool ProcessCore(T model);

        /// <summary>
        /// 通过反射调用
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>业务执行结果</returns>
        protected bool InternalProcess(T model)
        {
            //此处预留可以做干预
            bool result = ProcessCore(model);
            //此处预留可以做干预
            return result;
        }

        /// <summary>
        /// 获取生产者实例并启动它，它和生产者服务类实例不是一个东西，请勿混淆
        /// </summary>
        /// <returns></returns>
        protected IONSProducer GetProducer()
        {
            IONSProducer producer = ONSHelper.ONSProducerList.Where(p => (p.Type == this.MessageType.ToString().ToUpper()) && (p.ProducerId == this.Pid)).FirstOrDefault();
            if (producer == null)
            {
                //生产者对象不存在，则新建

                lock (_lockHelper)
                {
                    producer = ONSHelper.ONSProducerList.Where(p => (p.Type == this.MessageType.ToString().ToUpper()) && (p.ProducerId == this.Pid)).FirstOrDefault();
                    if (producer == null)
                    {
                        ONSFactoryProperty onsProducerFactoryProperty = new ONSFactoryProperty();
                        onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.AccessKey, _AliyunOnsAccessKey);
                        onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.SecretKey, _AliyunOnsSecretKey);
                        onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.ProducerId, this.Pid);
                        onsProducerFactoryProperty.setFactoryProperty(ONSFactoryProperty.PublishTopics, this.Topic);

                        producer = InitilizeProducer(onsProducerFactoryProperty);
                        ONSHelper.ONSProducerList.Add(producer);
                        producer.start();
                    }
                }
            }
            return producer;
        }

        /// <summary>
        /// 初始化生产者实例，此方法会在GetProducer()中被调用
        /// </summary>
        /// <param name="onsProducerFactoryProperty"></param>
        /// <returns></returns>
        protected abstract IONSProducer InitilizeProducer(ONSFactoryProperty onsProducerFactoryProperty);

        /// <summary>
        /// 尝试获取请求的requestTraceId
        /// </summary>
        /// <returns></returns>
        protected string GetRequestTraceId()
        {
            string requestTraceId = CallContext.LogicalGetData("TraceId") == null ? "" : CallContext.LogicalGetData("TraceId").ToString();
            if (requestTraceId != "")
            {
                //CallContext中存在TraceId
                return requestTraceId;
            }
            else
            {
                //CallContext中不存在TraceId
                if (HttpContext.Current != null)
                {
                    if (HttpContext.Current.Items != null)
                    {
                        if (HttpContext.Current.Items.Contains("TraceId"))
                        {
                            requestTraceId = HttpContext.Current.Items["TraceId"].ToString();
                        }
                    }
                }
            }
            return requestTraceId;
        }

        /// <summary>
        /// 尝试发送消息
        /// </summary>
        /// <param name="producer">生产者实例</param>
        /// <param name="message">消息实例</param>
        /// <param name="parameter">发送所需参数</param>
        /// <param name="key">消息的唯一标识</param>
        /// <param name="errorTimes">本系统自己自己执行时发现出错后，记录的错误次数（此参数不适用于生产次数）</param>
        /// <returns></returns>
        protected SendResultONS TryToSend(IONSProducer producer, Message message, object parameter, string key, int errorTimes)
        {
            SendResultONS sendResultONS = null;

            try
            {
                sendResultONS = producer.send(message, parameter);
            }
            catch (Exception e)
            {
                //错误计数累加
                errorTimes++;
                //无论是阿里云服务不可用，还是生产者挂了，一共3次机会，即执行1次，然后最多重试两次                 
                if (errorTimes < 3)
                {
                    //如果生产者挂了
                    if (e.ToString().IndexOf("Your producer has been shutdown.") >= 0)
                    {
                        //置空producer
                        producer = null;
                        //重新获取producer并启动
                        producer = GetProducer();
                        //等待3秒
                        System.Threading.Thread.Sleep(3000);
                    }
                    else
                    {
                        //如果是阿里云服务不可用，等待1秒
                        System.Threading.Thread.Sleep(1000);
                    }

                    //递归
                    sendResultONS = TryToSend(producer, message, parameter, key, errorTimes);
                }
                else
                {
                    //重试2次都还是失败
                    string className = this.GetType().Name;
                    string methodName = "Process";
                    string errorMessage = _Environment + "." + _ApplicationAlias + "." + className + "." + methodName + "尝试发送时，出现了第" + errorTimes + "次出错，key=" + key + "：" + e.ToString();
                    //记录本地错误日志
                    DebugUtil.Debug(errorMessage);
                    //记录FATAL日志
                    ONSHelper.SaveLog(LogTypeEnum.FATAL, className, methodName, errorMessage);
                    //发送邮件
                    ONSHelper.SendDebugMail(_Environment + "." + _ApplicationAlias + "." + className + "." + methodName + "尝试发送时出错", errorMessage);
                    //抛出异常
                    throw new Exception(errorMessage);
                }
            }

            return sendResultONS;
        }
    }
}
