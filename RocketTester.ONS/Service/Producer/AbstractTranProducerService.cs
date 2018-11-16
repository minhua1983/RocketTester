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
    public abstract class AbstractTranProducerService<T> : AbstractProducerService<T>, IAbstractProducerService
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
            int accomplishment = 0;
            int producedTimes = 0;
            try
            {
                //获取requestTraceId
                requestTraceId = this.GetRequestTraceId();
                //序列化实体，即消息正文
                body = model.GetType().Name.ToLower() == "system.string" ? model.ToString() : JsonConvert.SerializeObject(model);
                //防止中文乱码
                body = Base64Util.Encode(body);
                //Message实体的body不能为空
                Message message = new Message(this.Topic, this.Tag, body);
                message.setKey(key);
                message.putUserProperties("type", ONSMessageType.TRAN.ToString());
                message.putUserProperties("requestTraceId", requestTraceId);
                //message.putUserProperties("shardingKey", shardingKey);

                DebugUtil.Debug("topic " + this.Topic);
                DebugUtil.Debug("tag " + this.Tag);



                //获取InternalProcess方法
                MethodInfo methodInfo = this.GetType().GetMethod("InternalProcess", BindingFlags.NonPublic | BindingFlags.Instance);

                DebugUtil.Debug("this.GetType().name " + this.GetType().FullName);

                DebugUtil.Debug("body " + body);
                DebugUtil.Debug("methodInfo.Name " + methodInfo.Name);
                DebugUtil.Debug("methodInfo.ReflectedType.FullName " + methodInfo.ReflectedType.FullName);


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

                DebugUtil.Debug("get ready to send message...");

                //生成半消息，并调用LocalTransactionExecuter对象的execute方法，它内部会执行委托实例（同时会将执行后的TransactionResult以Message的key为redis的key存入redis中），根据执行结果再决定是否要将消息状态设置为rollback或commit
                IONSProducer transactionProducer = GetProducer();
                SendResultONS sendResultONS = null;
                try
                {
                    sendResultONS = transactionProducer.send(message, executer);
                }
                catch (Exception e)
                {
                    string className = this.GetType().Name;
                    string methodName = "Process";

                    //记录本地错误日志
                    DebugUtil.Debug(_Environment + "." + _ApplicationAlias + "." + className + "." + methodName + "出错，key=" + key+"：" + e.ToString());

                    //记录FATAL日志
                    ONSHelper.SaveLog(LogTypeEnum.FATAL, className, methodName, _Environment + "." + _ApplicationAlias + "." + className + "." + methodName + "出错，key=" + key + "：" + e.ToString());

                    //发送邮件
                    ONSHelper.SendDebugMail(_Environment + "." + _ApplicationAlias + "." + className + "." + methodName + "出错", "key=" + key + "：" + e.ToString());
                }
                if (sendResultONS == null)
                {
                    throw new Exception("发送TRAN消息失败。key=" + key);
                }
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
