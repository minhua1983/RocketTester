using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using System.Configuration;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using Newtonsoft.Json;
using ons;
using Redis.Framework;
using Nest.Framework;
using Liinji.Common;

namespace RocketTester.ONS
{
    public class ONSLocalTransactionChecker : LocalTransactionChecker
    {
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        static int _AliyunOnsRedisDbNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["AliyunOnsRedisDbNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["AliyunOnsRedisDbNumber"]);
        static int _AliyunOnsRedisServiceResultExpireIn = string.IsNullOrEmpty(ConfigurationManager.AppSettings["AliyunOnsRedisServiceResultExpireIn"]) ? 86400 : int.Parse(ConfigurationManager.AppSettings["AliyunOnsRedisServiceResultExpireIn"]);
        static string _ApplicationAlias = ConfigurationManager.AppSettings["ApplicationAlias"] ?? "unknown";

        public ONSLocalTransactionChecker()
        {

        }

        ~ONSLocalTransactionChecker()
        {
        }

        public override TransactionStatus check(Message value)
        {
            Console.WriteLine("check topic: {0}, tag:{1}, key:{2}, msgId:{3},msgbody:{4}, userProperty:{5}",
            value.getTopic(), value.getTag(), value.getKey(), value.getMsgID(), value.getBody(), value.getUserProperties("VincentNoUser"));
            // 消息 ID(有可能消息体一样，但消息 ID 不一样。当前消息 ID 在控制台无法查询)
            //string msgId = value.getMsgID();
            // 消息体内容进行 crc32, 也可以使用其它的如 MD5
            // 消息 ID 和 crc32id 主要是用来防止消息重复
            // 如果业务本身是幂等的， 可以忽略，否则需要利用 msgId 或 crc32Id 来做幂等
            // 如果要求消息绝对不重复，推荐做法是对消息体 body 使用 crc32或 md5来防止重复消息 

            string transactionType = "Checker";
            bool serviceResult = false;
            TransactionStatus transactionStatus = TransactionStatus.Unknow;
            string failureReason = "";
            string topic = "";
            string tag = "";
            string pid = "";
            string key = "";
            string body = "";
            string checkerMethodParameter = "";
            string method = "";
            string requestTraceId = "";
            string producedTimesKey = "";
            int producedTimes = 0;

            try
            {
                topic = value.getTopic();
                tag = value.getTag();
                pid = "PID_" + value.getTopic().ToUpper();
                key = value.getKey();
                body = value.getMsgBody();
                requestTraceId = value.getUserProperties("requestTraceId") ?? "";
                checkerMethodParameter = Base64Util.Decode(body);
                producedTimesKey = key + ":" + _ApplicationAlias + ":producedtimes";

                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionChecker.check.key  " + key);

                /*在LocalTransactionExecuterExecute.execute方法中返回unknown，之后会调用LocalTransactionChecker.check方法
                 * 如果此消息topic被多个网站用作生产者，那么会导致此message会被随即传递到其他网站或当前网站中去时长调用check方法
                 * 这种传递紊乱的情况可以称为“同topic被多站点作为生产者导致check紊乱的现象”
                 * 因此如果被传递到其他网站后，需要判断当前网站是否是消息的来源网站，如果不是则返回unknown，也不记录任何记录。
                 * 如果按同topic被5个网站作为生产者，那么当execute或check出错后，能正确来到消息原始网站的概率为20%，而每次调用check历经5秒
                 * 假设是随即平均发送到5个网站随即一个中，那么平均需要历经25秒后，才能来到原始网站，因此能来到消息原始网站的平均等待时间的公式为
                 * time=n*5秒，此处n为使用该topic的网站个数。
                //*/
                int applicationAliasLength = _ApplicationAlias.Length;
                if (key.Substring(2, applicationAliasLength) != _ApplicationAlias)
                {
                    return transactionStatus;
                }

                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionChecker.check.after try...");
                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionChecker.check.checkerMethod " + value.getUserProperties("checkerMethod"));
                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionChecker.check.checkerMethodParameter " + value.getUserProperties("checkerMethodParameter"));

                if (ONSHelper.CheckerMethodDictionary.ContainsKey(value.getUserProperties("checkerMethod")))
                {
                    MethodInfo methodInfo = ONSHelper.CheckerMethodDictionary[value.getUserProperties("checkerMethod")];
                    Type type = methodInfo.ReflectedType;
                    Assembly assembly = Assembly.GetAssembly(type);
                    object service = assembly.CreateInstance(type.FullName);
                    ParameterInfo[] parameterInfos = methodInfo.GetParameters();

                    //DebugUtil.Log(parameterInfos[0].ParameterType.ToString());

                    method = type.FullName + ".ProcessCore";

                    //判断类型
                    if (parameterInfos[0].ParameterType.ToString().ToLower() == "system.string")
                    {
                        //string类型
                        serviceResult = (bool)methodInfo.Invoke(service, new object[] { checkerMethodParameter });
                    }
                    else
                    {
                        //自定义类型
                        object parameter = JsonConvert.DeserializeObject(checkerMethodParameter, parameterInfos[0].ParameterType);
                        serviceResult = (bool)methodInfo.Invoke(service, new object[] { parameter });
                    }

                    //DebugUtil.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionChecker.check.body:" + body);

                    if (serviceResult)
                    {
                        // 本地事务成功则提交消息
                        transactionStatus = TransactionStatus.CommitTransaction;
                    }
                    else
                    {
                        // 本地事务失败则回滚消息
                        transactionStatus = TransactionStatus.RollbackTransaction;
                        failureReason = method + "执行返回serviceResult为false，可能是该方法逻辑上返回serviceResult为false，也可能是该方法执行时它自己捕捉到错误返回serviceResult为false";
                    }
                }
                else
                {
                    // 不存在key则回滚消息，这里不能用RollbackTransaction，因为我们使用相同的topic在各个网站之间，这种机制会导致其他网站也会调用check方法，一旦RollbackTransaction，那么原先的网站不再重试调用check方法了，因此必须返回unknow
                    transactionStatus = TransactionStatus.Unknow;
                    failureReason = "CheckerFuncDictionary中不存在key:" + value.getUserProperties("checkerMethod");
                    DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionChecker.check.error:CheckerFuncDictionary中不存在key:" + value.getUserProperties("checkerMethod"));
                }

                //在写log之前先做base64解密
                body = Base64Util.Decode(body);

                //事务已经执行，尝试通过redis更新生产方法执行次数，此处如果出错是可以容忍的，但是得把错误信息记录到ProduceData中
                RedisTool RT = new RedisTool(_AliyunOnsRedisDbNumber, _RedisExchangeHosts);
                if (RT != null)
                {
                    try
                    {
                        //获取已经生产次数
                        string producedTimesValue = RT.StringGet(producedTimesKey);
                        //如果取不到
                        if (string.IsNullOrEmpty(producedTimesValue))
                        {
                            //不存在key，则新增
                            producedTimes++;
                            bool isSaved = RT.StringSet(producedTimesKey, producedTimes, TimeSpan.FromSeconds(_AliyunOnsRedisServiceResultExpireIn));
                            if (!isSaved)
                            {
                                transactionStatus = TransactionStatus.Unknow;
                            }
                        }
                        else
                        {
                            //存在key，则递增
                            int.TryParse(producedTimesValue, out producedTimes);
                            producedTimes++;
                            RT.StringIncrement(producedTimesKey);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception("事务已经执行，返回" + serviceResult + "。但是尝试通过redis更新生产方法执行次数时，捕捉异常：" + e.ToString());
                    }
                }
                else
                {
                    //redis有问题，导致无法实例化工具类
                    throw new Exception("事务已经执行，返回" + serviceResult + "。但是尝试通过redis更新生产方法执行次数时，无法实例化redis工具类，可能是redis服务暂不可用。");
                }
            }
            catch (Exception e)
            {
                failureReason = "事务消息在执行Checker.check方法时捕获异常：" + e.ToString();
                DebugUtil.Debug(e.ToString());
            }
            finally
            {
                ProducerData producerData = new ProducerData(requestTraceId);
                producerData.Accomplishment = transactionStatus == TransactionStatus.CommitTransaction ? 1 : 0;
                producerData.ApplicationAlias = _ApplicationAlias;
                producerData.Topic = topic;
                producerData.Tag = tag;
                producerData.ProducerId = pid;
                producerData.Key = key;
                producerData.Type = ONSMessageType.TRAN.ToString();
                producerData.Message = checkerMethodParameter;
                producerData.TransactionType = transactionType;
                producerData.Method = method;
                producerData.ServiceResult = serviceResult;
                producerData.TransactionStatus = transactionStatus.ToString();
                producerData.FailureReason = failureReason;
                producerData.ProducedTimes = producedTimes;
                producerData.ShardingKey = "";
                NestDataHelper.WriteData(producerData);
            }

            return transactionStatus;
        }
    }
}
