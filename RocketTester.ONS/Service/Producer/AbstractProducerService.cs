using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using ons;
using Redis.Framework;
using Newtonsoft.Json;
using Nest.Framework;

namespace RocketTester.ONS
{
    public abstract class AbstractProducerService<T> : IAbstractProducerService<T>
    {
        //redis地址
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        //启用redis第N个数据库
        static int _ONSRedisDBNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisDBNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["ONSRedisDBNumber"]);
        //redis中key的过期时间，目前设置了24小时
        static int _ONSRedisServiceResultExpireIn = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisServiceResultExpireIn"]) ? 86400 : int.Parse(ConfigurationManager.AppSettings["ONSRedisServiceResultExpireIn"]);
        //获取当前环境，p代表生产环境production，s代表测试环境staging，d代表开发环境development
        static string _Environment = ConfigurationManager.AppSettings["Environment"] ?? "p";
        static string _ApplicationAlias = ConfigurationManager.AppSettings["ApplicationAlias"] ?? "unknown";

        /*
        public ONSMessageTopic Topic { get; private set; }
        public ONSMessageTag Tag { get; private set; }

        public AbstractProducerService(ONSMessageTopic topic, ONSMessageTag tag)
        {
            Topic = topic;
            Tag = tag;
        }
        //*/

        public Enum TopicTag { get; private set; }
        public AbstractProducerService(Enum topicTag)
        {
            TopicTag = topicTag;
        }

        /// <summary>
        /// ProcessCore抽象方法，主要用于派生类重写它逻辑，即上游生产者事务方法。
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>事务执行结果</returns>
        protected abstract ServiceResult ProcessCore(T model);

        /// <summary>
        /// 通过反射调用
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>事务执行结果</returns>
        protected ServiceResult InternalProcess(T model)
        {
            //此处预留可以做干预
            ServiceResult result = ProcessCore(model);
            //此处预留可以做干预
            return result;
        }

        /// <summary>
        /// 上游生产者的实现rocketmq的核心方法，其中会由rocketmq自动间接调用AbstractProducerService实例的InternalProduce方法
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>事务执行结果</returns>
        public ServiceResult Process(T model)
        {
            ServiceResult serviceResult = InternalProcess(model);

            string topic = (_Environment + "_" + TopicTag.GetType().Name).ToLower();
            string tag = TopicTag.ToString();
            string pid = ("PID_" + topic).ToUpper();
            string key = _Environment + "_" + _ApplicationAlias + ":" + topic + ":" + tag + ":" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ":" + Guid.NewGuid().ToString();
            string data = JsonConvert.SerializeObject(serviceResult.Data);
            string body = "no content";
            string method = this.GetType().FullName + ".ProcessCore";
            string failureReason = "";
            string shardingKey = "";

            if (serviceResult.Pushable)
            {
                //如果需要发送消息

                RedisTool RT;
                string result = JsonConvert.SerializeObject(serviceResult);

                IONSProducer producer = ONSHelper.ONSProducerList.Where(p => (p.Type == ONSMessageType.BASE.ToString().ToUpper()) && (p.ProducerId == pid)).FirstOrDefault();
                if (producer != null)
                {
                    Message message = new Message(topic, tag, body);
                    message.setKey(key);
                    message.putUserProperties("type", ONSMessageType.BASE.ToString());
                    message.putUserProperties("shardingKey", shardingKey);

                    LogHelper.Log("send:" + key);
                    SendResultONS sendResultONS = producer.send(message, null);

                    try
                    {
                        RT = new RedisTool(_ONSRedisDBNumber, _RedisExchangeHosts);
                        RT.StringSet(key, result, TimeSpan.FromSeconds(_ONSRedisServiceResultExpireIn));
                    }
                    catch (Exception e)
                    {
                        failureReason = "捕捉异常：" + e.ToString();
                        LogHelper.Log("捕捉异常：" + e.ToString());
                    }
                }
            }

            ProduceData produceData = new ProduceData();
            produceData.Accomplishment = 1;
            produceData.ApplicationAlias = _ApplicationAlias;
            produceData.Topic = topic;
            produceData.Tag = tag;
            produceData.ProducerId = pid;
            produceData.Key = key;
            produceData.Type = ONSMessageType.BASE.ToString();
            produceData.Message = body;
            produceData.Data = data;
            produceData.TransactionType = "";
            produceData.Method = method;
            produceData.ServiceResult = JsonConvert.SerializeObject(serviceResult);
            produceData.TransactionStatus = "";
            produceData.FailureReason = failureReason;
            produceData.ProducedTimes = 1;
            produceData.Pushable = serviceResult.Pushable ? 1 : 0;
            produceData.ShardingKey = shardingKey;
            NestDataHelper.WriteData(produceData);

            return serviceResult;
        }
    }
}
