using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Web;
using System.Text;
using ons;
using Newtonsoft.Json;
using Nest.Framework;
using Liinji.Common;

namespace RocketTester.ONS
{
    public abstract class AbstractBaseProducerService<T> : AbstractProducerService<T>
    {
        public AbstractBaseProducerService(Enum topicTag)
            : base(ONSMessageType.BASE, topicTag)
        {

        }

        /// <summary>
        /// 上游生产者的实现rocketmq的核心方法，其中会由rocketmq自动间接调用AbstractProducerService实例的InternalProduce方法
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>事务执行结果</returns>
        public bool Process(T model)
        {
            if (_AliyunOnsIsAllowedToSend != "1") { return false; }
            string requestTraceId = "";
            string key = this.CreateMessageKey();
            string failureReason = "";
            string body = "";
            bool accomplishment = false;
            int producedTimes = 0;
            int errorTimes = 0;
            try
            {
                //获取requestTraceId
                requestTraceId = this.GetRequestTraceId();
                //此方法中，只要是普通消息或顺序消息都是空实现，直接返回true
                InternalProcess(model);
                //序列化实体，即消息正文
                body = model.GetType().Name.ToLower() == "system.string" ? model.ToString() : JsonConvert.SerializeObject(model);
                //防止中文乱码
                body = Base64Util.Encode(body);
                //获取生产者
                IONSProducer producer = GetProducer();
                //生成消息实体
                Message message = new Message(this.Topic, this.Tag, body);
                message.setKey(key);
                message.putUserProperties("type", this.MessageType.ToString());
                message.putUserProperties("requestTraceId", requestTraceId);
                //message.putUserProperties("shardingKey", shardingKey);
                //定义消息结果对象
                SendResultONS sendResultONS = null;
                //尝试发送
                sendResultONS = TryToSend(producer, message, null, key, errorTimes);
                //判断结果，一般走不到这步，因为如果发送有问题会直接抛出异常的
                if (sendResultONS == null)
                {
                    throw new Exception("发送BASE消息失败。key=" + key);
                }
                //更新发送状态
                accomplishment = true;
                //更新消费次数
                producedTimes = 1;
            }
            catch (Exception e)
            {
                //将内部捕捉的错误赋值给failureReason，然后由ProduceData的FailureReason属性统一处理
                failureReason = "发送BASE消息，key=" + key + "，捕捉异常：" + e.ToString();
                return false;
            }
            finally
            {
                //无论是否有异常都统一写入生产日志
                LogData(key, body, "", "", failureReason, accomplishment, producedTimes, failureReason != "" ? false : true);
            }
            return true;
        }

        protected override IONSProducer InitilizeProducer(ONSFactoryProperty onsProducerFactoryProperty)
        {
            //实例化Producer
            ons.Producer baseProducer = ONSFactory.getInstance().createProducer(onsProducerFactoryProperty);
            //实例化代理类ONSProducer
            IONSProducer producer = new ONSBaseProducer(this.Topic, this.Pid, baseProducer);
            return producer;
        }
    }
}
