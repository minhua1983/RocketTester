using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Reflection;
using System.Collections.Concurrent;
using System.Configuration;
using System.Runtime.Remoting.Messaging;
using ons;
using Newtonsoft.Json;
using Liinji.Common;
using Nest.Framework;

namespace RocketTester.ONS
{
    public abstract class AbstractTranProducerService<T> : AbstractProducerService<T>
    {
        public AbstractTranProducerService(Enum topicTag)
            : base(ONSMessageType.TRAN, topicTag)
        {

        }



        /// <summary>
        /// 上游生产者的实现rocketmq的核心方法，其中会由rocketmq自动间接调用AbstractTransactionProducerService实例的InternalProduce方法
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
                //序列化实体，即消息正文
                body = model.GetType().Name.ToLower() == "system.string" ? model.ToString() : JsonConvert.SerializeObject(model);
                //防止中文乱码
                body = Base64Util.Encode(body);
                //获取生产者
                IONSProducer producer = GetProducer();
                //Message实体的body不能为空
                Message message = new Message(this.Topic, this.Tag, body);
                message.setKey(key);
                message.putUserProperties("type", ONSMessageType.TRAN.ToString());
                message.putUserProperties("requestTraceId", requestTraceId);

                //获取InternalProcess方法
                MethodInfo methodInfo = this.GetType().GetMethod("InternalProcess", BindingFlags.NonPublic | BindingFlags.Instance);

                string executerMethodName = methodInfo.ReflectedType.FullName + "." + methodInfo.Name;
                string checkerMethodName = methodInfo.ReflectedType.FullName + "." + methodInfo.Name;

                //将方式实例和方式实例的参数都存到消息的属性中去。
                //message.putUserProperties("executerMethodParameter", data);
                message.putUserProperties("executerMethod", executerMethodName);
                //message.putUserProperties("checkerMethodParameter", data);
                message.putUserProperties("checkerMethod", checkerMethodName);

                //方式实例字典中不存在的话，则试图新增到字典中去
                if (!ONSHelper.ExecuterMethodDictionary.ContainsKey(executerMethodName))
                {
                    ONSHelper.ExecuterMethodDictionary.TryAdd(executerMethodName, methodInfo);
                }
                if (!ONSHelper.CheckerMethodDictionary.ContainsKey(checkerMethodName))
                {
                    ONSHelper.CheckerMethodDictionary.TryAdd(checkerMethodName, methodInfo);
                }

                //实例化LocalTransactionExecuter对象
                ONSLocalTransactionExecuter executer = new ONSLocalTransactionExecuter();
                //定义消息结果对象
                SendResultONS sendResultONS = null;
                //尝试发送
                sendResultONS = TryToSend(producer, message, executer, key, errorTimes);
                //判断结果，一般走不到这步，因为如果发送有问题会直接抛出异常的
                if (sendResultONS == null)
                {
                    throw new Exception("发送TRAN消息失败。key=" + key);
                }
                //更新发送状态
                accomplishment = true;
                //更新消费次数
                producedTimes = 1;
            }
            catch (Exception e)
            {
                failureReason = "发送TRAN消息，key=" + key + "，在TranProducerService中(非Executer或Checker中)，捕获异常" + e.ToString();
                LogData(key, body, "", "", failureReason, accomplishment, producedTimes, false);
                return false;
            }

            return true;
        }

        protected override IONSProducer InitilizeProducer(ONSFactoryProperty onsProducerFactoryProperty)
        {
            //实例化ONSLocalTransactionChecker
            ONSLocalTransactionChecker checker = new ONSLocalTransactionChecker();
            //实例化TransactionProducer
            TransactionProducer transactionProducer = ONSFactory.getInstance().createTransactionProducer(onsProducerFactoryProperty, checker);
            //实例化代理类ONSTransactionProducer
            IONSProducer producer = new ONSTranProducer(this.Topic, this.Pid, transactionProducer);
            return producer;
        }
    }
}
