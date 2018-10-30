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
using Nest.Framework;

namespace RocketTester.ONS
{
    /// <summary>
    /// MyMessageListener类继承自ons.MessageListener。当pushConsumer订阅消息后，会调用consume。同一个网站下不允许不同的ConsumerServer使用相同的Topic，且Tag也相同
    /// </summary>
    public class ONSMessageListener : MessageListener
    {
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        static int _ONSRedisDBNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisDBNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["ONSRedisDBNumber"]);
        static int _ONSRedisServiceResultExpireIn = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisServiceResultExpireIn"]) ? 86400 : int.Parse(ConfigurationManager.AppSettings["ONSRedisServiceResultExpireIn"]);

        //获取当前环境，p代表生产环境production，s代表测试环境staging，d代表开发环境development
        static string _Environment = ConfigurationManager.AppSettings["Environment"] ?? "p";
        static string _ApplicationAlias = ConfigurationManager.AppSettings["ApplicationAlias"] ?? "unknown";

        public ONSMessageListener()
        {
            
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



            RedisTool RT = null;
            bool needToCommit = false;
            string data = "";
            string failureReason = "";
            string topic = value.getTopic();
            string tag = value.getTag();
            string pid = "PID_" + value.getTopic().ToUpper();
            string cid = ("CID_" + topic + "_" + _ApplicationAlias).ToUpper();
            string key = value.getKey();
            string messageType = value.getUserProperties("type");
            string body = value.getMsgBody();
            string method = "";
            string shardingKey = value.getUserProperties("shardingKey");
            Enum topicTag = null;

            try
            {
                RT = new RedisTool(_ONSRedisDBNumber, _RedisExchangeHosts);
                string result = RT.StringGet(key);

                ServiceResult serviceResult = JsonConvert.DeserializeObject<ServiceResult>(result);

                foreach (Type type in ONSHelper.ONSConsumerServiceList)
                {

                    object service = Assembly.GetAssembly(type).CreateInstance(type.FullName);
                    //string serviceTopic = type.GetProperty("Topic").GetValue(service).ToString();
                    //string serviceTag = type.GetProperty("Tag").GetValue(service).ToString();

                    Enum[] topicTagList = (Enum[])type.GetProperty("TopicTagList").GetValue(service);
                    if(topicTagList!=null)
                    {
                        //需要同时判断topic和tag都匹配
                        topicTag = topicTagList.Where(tt => {
                            string serviceTopic = (_Environment + "_" + tt.GetType().Name).ToUpper();
                            string serviceTag = tt.ToString();
                            return (topic.ToUpper() == serviceTopic) && (tag.ToUpper() == serviceTag.ToUpper());
                        }).FirstOrDefault();

                        if (topicTag != null)
                        {
                            method = type.FullName + ".ProcessCore";
                            //var parameter = service.ConvertStringToType(data);
                            MethodInfo methodInfo = type.GetMethod("InternalProcess", BindingFlags.NonPublic | BindingFlags.Instance);
                            //MethodInfo methodInfo = type.GetMethod("InternalProcess");

                            ParameterInfo[] parameterInfos = methodInfo.GetParameters();

                            LogHelper.Log(parameterInfos[0].ParameterType.ToString());

                            //判断类型
                            if (parameterInfos[0].ParameterType.ToString().ToLower() == "system.string")
                            {
                                //string类型
                                data = serviceResult.Data.ToString();
                                needToCommit = (bool)methodInfo.Invoke(service, new object[] { data });
                            }
                            else
                            {
                                //自定义类型
                                data = JsonConvert.SerializeObject(serviceResult.Data);
                                object parameter = JsonConvert.DeserializeObject(data, parameterInfos[0].ParameterType);
                                needToCommit = (bool)methodInfo.Invoke(service, new object[] { parameter });
                            }

                            if (needToCommit == false)
                            {
                                failureReason = method + "执行返回false，可能是该方法逻辑上返回false，也可能是该方法执行时它自己捕捉到错误返回false";
                            }

                            //needToCommit = service.Consume(data);
                            LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",needToCommit:" + needToCommit + "\n");

                            break;
                        }
                    }
                };
            }
            catch (Exception e)
            {
                //如果大量错误导致消息堆积，可以这里设置为true，但是还是不建议这样做，因为一般是因为Redis里面key的值发生了结构变化，在进行json反序列化时出错导致，此时如果设置为true，但是下游业务还未执行，这样从流程上来说是有问题的，应该第一时间发现，并把旧格式的数据，修改为新格式的数据，之后重新尝试发送到下游后即可消费。

                //也不能手动删除redis中的key，否则导致JsonConvert.DeserializeObject<TransactionResult>(result)出错
                //needToCommit = true; 
                failureReason = "捕获异常：" + e.ToString();
                LogHelper.Log(e.ToString());
            }
            //*/


            Action action;

            if (needToCommit)
            {
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ons.Action.CommitMessage...\n");
                action = ons.Action.CommitMessage;
            }
            else
            {
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ons.Action.ReconsumeLater...\n");
                action = ons.Action.ReconsumeLater;
            }




            //*

            try
            {
                if (RT != null)
                {
                    if (topicTag != null)
                    {
                        string consumedTimesKey = key + "_" + cid + "_consumedtimes";
                        string consumedTimesValue = RT.StringGet(consumedTimesKey);
                        int consumedTimes = 0;

                        //写BaseMsgData数据
                        ConsumeData consumeData = new ConsumeData();

                        if (string.IsNullOrEmpty(consumedTimesValue))
                        {
                            //不存在key，则新增
                            consumedTimes++;
                            bool isSaved = RT.StringSet(consumedTimesKey, consumedTimes, TimeSpan.FromSeconds(_ONSRedisServiceResultExpireIn));
                            if (!isSaved)
                            {
                                action = ons.Action.ReconsumeLater;
                            }
                        }
                        else
                        {
                            //存在key，则递增
                            int.TryParse(consumedTimesValue, out consumedTimes);
                            consumedTimes++;
                            RT.StringIncrement(consumedTimesKey);
                        }

                        //string data;
                        consumeData.ApplicationAlias = _ApplicationAlias;
                        consumeData.Accomplishment = action == Action.CommitMessage ? 1 : 0;
                        consumeData.Topic = topic;
                        consumeData.Tag = tag;
                        consumeData.ProducerId = pid;
                        consumeData.ConsumerId = cid;
                        consumeData.Key = key;
                        consumeData.Type = messageType;
                        consumeData.Message = body;
                        consumeData.Data = data;
                        consumeData.Method = method;
                        consumeData.FailureReason = failureReason;
                        consumeData.ConsumedStatus = action.ToString();
                        consumeData.ConsumedTimes = consumedTimes;
                        consumeData.ShardingKey = shardingKey;
                        NestDataHelper.WriteData(consumeData);
                        //*/
                    }
                }
            }
            catch (Exception e)
            {
                LogHelper.Log(e.ToString());
            }


            return action;
        }
    }
}
