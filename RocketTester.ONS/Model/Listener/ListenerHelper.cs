using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Reflection;
using System.Web;
using Action = ons.Action;
using ons;
using Redis.Framework;
using Newtonsoft.Json;
using Nest.Framework;
using Liinji.Common;

namespace RocketTester.ONS
{
    public class ListenerHelper
    {
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        static int _AliyunOnsRedisDbNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["AliyunOnsRedisDbNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["AliyunOnsRedisDbNumber"]);
        static int _AliyunOnsRedisServiceResultExpireIn = string.IsNullOrEmpty(ConfigurationManager.AppSettings["AliyunOnsRedisServiceResultExpireIn"]) ? 86400 : int.Parse(ConfigurationManager.AppSettings["AliyunOnsRedisServiceResultExpireIn"]);
        static string _Environment = ConfigurationManager.AppSettings["Environment"] ?? "p";
        static string _ApplicationAlias = ConfigurationManager.AppSettings["ApplicationAlias"] ?? "unknown";

        public static bool React(Message value)
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
                cid = ("CID_" + topic + "_" + _ApplicationAlias).ToUpper();
                key = value.getKey();
                type = value.getUserProperties("type");
                body = value.getMsgBody();
                body = Base64Util.Decode(body);
                requestTraceId = value.getUserProperties("requestTraceId") ?? "";
                shardingKey = value.getUserProperties("shardingKey") ?? "";

                object parameter;

                //在ONSConsumerServiceList中找到能匹配TopicTag的消费者服务类实例
                object service = ONSHelper.ONSConsumerServiceList.Where(s =>
                {
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

                //如果消费者服务类实例存在则消费消息
                if (service != null)
                {
                    method = service.GetType().FullName + ".ProcessCore";
                    //var parameter = service.ConvertStringToType(data);

                    MethodInfo methodInfo = service.GetType().GetMethod("InternalProcess", BindingFlags.NonPublic | BindingFlags.Instance);
                    //MethodInfo methodInfo = type.GetMethod("InternalProcess");

                    ParameterInfo[] parameterInfos = methodInfo.GetParameters();

                    DebugUtil.Debug(parameterInfos[0].ParameterType.ToString());

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

                    //needToCommit = service.Consume(data);
                    DebugUtil.Debug("MESSAGE_KEY:" + key + ",needToCommit:" + needToCommit + "\n");
                }
                else
                {
                    //找不到消费者实例对象
                    DebugUtil.Debug("MESSAGE_KEY:" + key + ",找不到消费者实例，topic：" + topic + "，tag：" + tag + "");
                }
            }
            catch (Exception e)
            {
                //如果大量错误导致消息堆积，可以这里设置为true，但是还是不建议这样做，因为一般是因为Redis里面key的值发生了结构变化，在进行json反序列化时出错导致，此时如果设置为true，但是下游业务还未执行，这样从流程上来说是有问题的，应该第一时间发现，并把旧格式的数据，修改为新格式的数据，之后重新尝试发送到下游后即可消费。

                //也不能手动删除redis中的key，否则导致JsonConvert.DeserializeObject<TransactionResult>(result)出错
                //needToCommit = true; 
                failureReason = "尝试消费时，key=" + key + "，捕获异常：" + e.ToString();
                DebugUtil.Debug(e.ToString());
            }
            //*/

            //尝试记录消费信息
            try
            {
                RedisTool RT = new RedisTool(_AliyunOnsRedisDbNumber, _RedisExchangeHosts);
                if (RT != null)
                {
                    if (topicTag != null)
                    {
                        string consumedTimesKey = key + "_" + cid + "_consumedtimes";
                        string consumedTimesValue = RT.StringGet(consumedTimesKey);

                        if (string.IsNullOrEmpty(consumedTimesValue))
                        {
                            //不存在key，则新增
                            consumedTimes++;
                            bool isSaved = RT.StringSet(consumedTimesKey, consumedTimes, TimeSpan.FromSeconds(_AliyunOnsRedisServiceResultExpireIn));
                            if (!isSaved)
                            {
                                //设置消费次数失败
                                failureReason = "设置消费次数失败。";
                            }
                        }
                        else
                        {
                            //存在key，则递增
                            int.TryParse(consumedTimesValue, out consumedTimes);
                            consumedTimes++;
                            RT.StringIncrement(consumedTimesKey);
                        }
                    }
                    else
                    {
                        failureReason = "未在ONSConsumerServiceList中找到匹配的TopicTag。";
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
                DebugUtil.Debug(e.ToString());
            }
            finally
            {
                //写ConsumerData数据
                ConsumerData consumerData = new ConsumerData(requestTraceId);
                //string data;
                consumerData.ApplicationAlias = _ApplicationAlias;
                consumerData.Accomplishment = needToCommit ? 1 : 0;
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
                NestDataHelper.WriteData(consumerData);
            }

            return needToCommit;
        }
    }
}
