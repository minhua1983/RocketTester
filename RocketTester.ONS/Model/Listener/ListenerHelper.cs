using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Reflection;
using System.Web;
using System.Runtime.Remoting.Messaging;
using Action = ons.Action;
using ons;
using Redis.Framework;
using Newtonsoft.Json;
using Nest.Framework;
using Liinji.Common;
using StackExchange.Redis;

namespace RocketTester.ONS
{
    public class ListenerHelper
    {
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        static int _AliyunOnsRedisDbNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["AliyunOnsRedisDbNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["AliyunOnsRedisDbNumber"]);
        static int _AliyunOnsRedisServiceResultExpireIn = string.IsNullOrEmpty(ConfigurationManager.AppSettings["AliyunOnsRedisServiceResultExpireIn"]) ? 86400 : int.Parse(ConfigurationManager.AppSettings["AliyunOnsRedisServiceResultExpireIn"]);
        static string _Environment = ConfigurationManager.AppSettings["Environment"] ?? "s";
        static string _ApplicationAlias = ConfigurationManager.AppSettings["ApplicationAlias"] ?? "unknown";

        public static bool React(Message value, Type classType)
        {
            bool needToCommit = false;
            string failureReason = "";
            string topic = "";
            string tag = "";
            string pid = "";
            string cid = "";
            string key = "";
            string type = "";
            string body = "";
            string method = "";
            string requestTraceId = "";
            string shardingKey = "";
            Enum topicTag = null;
            int consumedTimes = 0;

            //尝试找到消费者服务类实例来消费
            try
            {
                topic = value.getTopic();
                tag = value.getTag();
                pid = "PID_" + value.getTopic().ToUpper();
                cid = ("CID_" + topic + "_" + _ApplicationAlias + "_" + classType.Name).ToUpper();
                key = value.getKey();
                type = value.getUserProperties("type");
                body = value.getMsgBody();
                body = Base64Util.Decode(body);
                requestTraceId = value.getUserProperties("requestTraceId") ?? "";
                shardingKey = value.getUserProperties("shardingKey") ?? "";

                //由于CallContext中不存在TraceId，则直接添加，以便在非http请求环境下获取到TraceId
                CallContext.LogicalSetData("TraceId", requestTraceId);

                object parameter;

                RedisTool RT = new RedisTool(_AliyunOnsRedisDbNumber, _RedisExchangeHosts);
                if (RT != null)
                {
                    string antirepeatKey = key + "_" + cid + "_antirepeat";
                    DateTime dateTime = DateTime.Now;
                    TimeSpan timeSpan = dateTime.AddSeconds(1) - dateTime;
                    bool setResult = RT.StringSet(antirepeatKey, "1", timeSpan, When.NotExists);
                    if (!setResult)
                    {
                        //如果设置失败，则说明key已经存在，本消息属于重复消费，直接返回true，不执行后面的消费逻辑
                        return true;
                    }
                    /*
                    //在ONSConsumerServiceList中找到能匹配TopicTag的消费者服务类实例
                    object service = ONSHelper.ONSConsumerServiceList.Where(s =>
                    {
                        string className = s.GetType().Name;
                        IAbstractConsumerService iservice = (IAbstractConsumerService)s;
                        Enum[] topicTagList = iservice.TopicTagList;
                        if (topicTagList != null)
                        {
                            //需要同时判断topic和tag都匹配
                            topicTag = topicTagList.Where(tt =>
                            {
                                string serviceTopic = (_Environment + "_" + tt.GetType().Name).ToUpper();
                                string serviceTag = tt.ToString();
                                return (topic.ToUpper() == serviceTopic) && (tag.ToUpper() == serviceTag.ToUpper());
                            }).FirstOrDefault();

                            if (topicTag != null)
                            {
                                return true;
                            }
                        }
                        return false;
                    }).FirstOrDefault();
                    //*/

                    object service = ONSHelper.ONSConsumerServiceList.Where(s => s.GetType().Name == classType.Name).FirstOrDefault();

                    //如果消费者服务类实例存在则消费消息
                    if (service != null)
                    {
                        //获取消费服务类的核心方法（即开发者自己实现的方法）
                        method = service.GetType().FullName + ".ProcessCore";
                        //获取内部方法（此方法是受保护的，因此获取MethodInfo复杂一些）
                        MethodInfo methodInfo = service.GetType().GetMethod("InternalProcess", BindingFlags.NonPublic | BindingFlags.Instance);
                        //获取参数列表，实际就一个泛型T参数
                        ParameterInfo[] parameterInfos = methodInfo.GetParameters();
                        //判断类型
                        if (parameterInfos[0].ParameterType.ToString().ToLower() == "system.string")
                        {
                            //string类型
                            parameter = body;
                        }
                        else
                        {
                            //自定义类型
                            parameter = JsonConvert.DeserializeObject(body, parameterInfos[0].ParameterType);
                        }
                        //执行InternalProcess方法
                        needToCommit = (bool)methodInfo.Invoke(service, new object[] { parameter });

                        if (needToCommit == false)
                        {
                            failureReason = method + "执行返回false，可能是该方法逻辑上返回false，也可能是该方法执行时它自己捕捉到错误返回false";
                        }
                    }
                    else
                    {
                        //找不到消费者实例对象
                        DebugUtil.Debug("MESSAGE_KEY:" + key + ",找不到消费者实例，topic：" + topic + "，tag：" + tag + "");
                    }
                }
                else
                {
                    failureReason = "尝试通过redis写入“key来做避免重复消费的判断”时，无法实例化redis工具类，可能是redis服务暂不可用。";
                }
            }
            catch (Exception e)
            {
                failureReason = "尝试消费时，key=" + key + "，捕获异常：" + e.ToString();
                //DebugUtil.Debug(e.ToString());
            }
            //*/

            //尝试记录消费信息
            try
            {
                //获取redis客户端工具实例
                RedisTool RT = new RedisTool(_AliyunOnsRedisDbNumber, _RedisExchangeHosts);
                if (RT != null)
                {
                    string consumedTimesKey = key + "_" + cid + "_consumedtimes";
                    //设置消费次数并自增
                    consumedTimes = (int)RT.StringIncrement(consumedTimesKey);
                    //获取过期时间
                    TimeSpan? timeSpan = RT.KeyTimeToLive(consumedTimesKey);
                    if (timeSpan == null)
                    { 
                        //没设置过过期时间，则设置过期时间
                        RT.KeyExpire(consumedTimesKey, TimeSpan.FromSeconds(_AliyunOnsRedisServiceResultExpireIn));
                    }
                }
                else
                {
                    failureReason = "尝试通过redis更新生产方法执行次数时，无法实例化redis工具类，可能是redis服务暂不可用。";
                }
            }
            catch (Exception e)
            {
                failureReason = "尝试通过redis更新生产方法执行次数时，捕捉异常：" + e.ToString();
                //DebugUtil.Debug(e.ToString());
            }
            finally
            {
                try
                {
                    //写ConsumerData数据
                    ConsumerData consumerData = new ConsumerData(requestTraceId);
                    //string data;
                    consumerData.ApplicationAlias = _ApplicationAlias;
                    consumerData.Accomplishment = needToCommit;
                    consumerData.Topic = topic;
                    consumerData.Tag = tag;
                    consumerData.ProducerId = pid;
                    consumerData.ConsumerId = cid;
                    consumerData.Key = key;
                    consumerData.Type = type;
                    consumerData.Message = body;
                    consumerData.Method = method;
                    consumerData.FailureReason = failureReason;
                    consumerData.ConsumedStatus = needToCommit ? "Commit" : "Reconsume";
                    consumerData.ConsumedTimes = consumedTimes;
                    consumerData.ShardingKey = shardingKey;
                    consumerData.ServerIp = ONSHelper.GetServerIp();
                    NestDataHelper.WriteData(consumerData);
                }
                catch (Exception e)
                {
                    ONSHelper.SendDebugMail(_Environment + "." + _ApplicationAlias + "环境发送下游消费日志失败", "消息key:" + key + "，错误信息如下：" + e.ToString());
                }
            }

            return needToCommit;
        }
    }
}
