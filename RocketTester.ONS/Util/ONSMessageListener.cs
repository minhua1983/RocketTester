using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Web;
using System.Reflection;
using System.Collections.Concurrent;
using Action = ons.Action;
using ons;
using Redis.Framework;
using Newtonsoft.Json;
using RocketTester.ONS.Model;

namespace RocketTester.ONS.Util
{
    /// <summary>
    /// MyMessageListener类继承自ons.MessageListener。当pushConsumer订阅消息后，会调用consume
    /// </summary>
    public class ONSMessageListener : MessageListener
    {
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        static int _ONSRedisDBNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisDBNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["ONSRedisDBNumber"]);

        public ONSMessageListener()
        {
            LogHelper.Log("MyMessageListener");
        }

        ~ONSMessageListener()
        {
        }

        public override Action consume(Message value, ConsumeContext context)
        {
            /*
            // Message 包含了消费到的消息，通过 getBody 接口可以拿到消息体
            Console.WriteLine("time:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("body:" + value.getMsgBody());
            Console.WriteLine("funcReturn:" + value.getUserProperties("funcReturn"));
            Console.WriteLine();
            //*/
            /*
                   所有中文编码相关问题都在 SDK 压缩包包含的文档里做了说明，请仔细阅读
            */


            //*
            LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",consume...");





            string key = value.getKey();
            //LogHelper.Log("key..." + key);
            //通过Tag来确认调用下游哪个方法

            string tag = value.getTag();


            RedisTool RT = new RedisTool(_ONSRedisDBNumber, _RedisExchangeHosts);
            string result = RT.StringGet(key);

            bool needToCommit = false;

            try
            {
                ONSTransactionResult transactionResult = JsonConvert.DeserializeObject<ONSTransactionResult>(result);


                string data = transactionResult.Data;

                //if (json == null) return ons.Action.ReconsumeLater; 

                LogHelper.Log(data);




                ONSHelper.ONSConsumerMethodInfoList.ForEach(methodInfo =>
                {
                    IEnumerable<ONSConsumerAttribute> onsConsumerAttributes = methodInfo.GetCustomAttributes<ONSConsumerAttribute>();
                    if (onsConsumerAttributes != null)
                    {
                        foreach (ONSConsumerAttribute oneConsumerAttribute in onsConsumerAttributes)
                        {
                            if (tag.Trim().ToLower() == oneConsumerAttribute.Tag.ToString().Trim().ToLower())
                            {
                                Type type = methodInfo.ReflectedType;
                                Assembly assembly = Assembly.GetAssembly(type);
                                object o = assembly.CreateInstance(type.FullName);
                                object[] os = new object[1] { data };
                                needToCommit = (bool)methodInfo.Invoke(o, os);
                                LogHelper.Log("MESSAGE_KEY:" + value .getKey()+ ",needToCommit:" + needToCommit + "\n");
                            }
                        }
                    }
                });
            }
            catch (Exception e)
            {
                //如果大量错误导致消息堆积，可以这里设置为true，但是还是不建议这样做，因为一般是因为Redis里面key的值发生了结构变化，在进行json反序列化时出错导致，此时如果设置为true，但是下游业务还未执行，这样从流程上来说是有问题的，应该第一时间发现，并把旧格式的数据，修改为新格式的数据，之后重新尝试发送到下游后即可消费。

                //也不能手动删除redis中的key，否则导致JsonConvert.DeserializeObject<TransactionResult>(result)出错
                //needToCommit = true; 

                LogHelper.Log(e.ToString());
            }
            //*/
            if (needToCommit)
            {
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ons.Action.CommitMessage...\n");
                return ons.Action.CommitMessage;
            }
            else
            {
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ons.Action.ReconsumeLater...\n");
                return ons.Action.ReconsumeLater;
            }
        }
    }
}
