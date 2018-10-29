using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using System.Configuration;
using System.Reflection;
using Newtonsoft.Json;
using ons;
using Redis.Framework;
using RocketTester.ONS.Enum;
using RocketTester.ONS.Model;
using RocketTester.ONS.Service;
using RocketTester.ONS.Util;
using Nest.Framework;

namespace RocketTester.ONS.Model
{

    public class ONSLocalTransactionExecuter : LocalTransactionExecuter
    {
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        static int _ONSRedisDBNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisDBNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["ONSRedisDBNumber"]);
        static int _ONSRedisServiceResultExpireIn = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisServiceResultExpireIn"]) ? 86400 : int.Parse(ConfigurationManager.AppSettings["ONSRedisServiceResultExpireIn"]);
        static string _ApplicationAlias = ConfigurationManager.AppSettings["ApplicationAlias"] ?? "unknown";

        public ONSLocalTransactionExecuter()
        {

        }

        ~ONSLocalTransactionExecuter()
        {
        }

        public override TransactionStatus execute(Message value)
        {
            Console.WriteLine("execute topic: {0}, tag:{1}, key:{2}, msgId:{3},msgbody:{4}, userProperty:{5}",
            value.getTopic(), value.getTag(), value.getKey(), value.getMsgID(), value.getBody(), value.getUserProperties("VincentNoUser"));
            // 消息 ID(有可能消息体一样，但消息 ID 不一样。当前消息 ID 在控制台无法查询)
            //string msgId = value.getMsgID();
            // 消息体内容进行 crc32, 也可以使用其它的如 MD5
            // 消息 ID 和 crc32id 主要是用来防止消息重复
            // 如果业务本身是幂等的， 可以忽略，否则需要利用 msgId 或 crc32Id 来做幂等
            // 如果要求消息绝对不重复，推荐做法是对消息体 body 使用 crc32或 md5来防止重复消息

            RedisTool RT = null;
            ServiceResult serviceResult = new ServiceResult();
            TransactionStatus transactionStatus = TransactionStatus.Unknow;
            string data = "";
            string failureReason = "";
            string topic = value.getTopic();
            string tag = value.getTag();
            string pid = "PID_" + value.getTopic().ToUpper();
            string key = value.getKey();
            string body = value.getMsgBody();
            string method = "";
            string shardingKey = value.getUserProperties("shardingKey");

            LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.key  " + key);

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

            try
            {
                RT = new RedisTool(_ONSRedisDBNumber, _RedisExchangeHosts);

                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.after try...");
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.executerMethod " + value.getUserProperties("executerMethod"));
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.executerMethodParameter " + value.getUserProperties("executerMethodParameter"));

                if (ONSHelper.ExecuterMethodDictionary.ContainsKey(value.getUserProperties("executerMethod")))
                {
                    MethodInfo methodInfo = ONSHelper.ExecuterMethodDictionary[value.getUserProperties("executerMethod")];
                    string executerMethodParameter = value.getUserProperties("executerMethodParameter");
                    Type type = methodInfo.ReflectedType;

                    Assembly assembly = Assembly.GetAssembly(type);
                    object service = assembly.CreateInstance(type.FullName);
                    ParameterInfo[] parameterInfos = methodInfo.GetParameters();

                    LogHelper.Log(parameterInfos[0].ParameterType.ToString());

                    method = type.FullName + ".ProcessCore";

                    //判断类型
                    if (parameterInfos[0].ParameterType.ToString().ToLower() == "system.string")
                    {
                        //string类型
                        serviceResult = (ServiceResult)methodInfo.Invoke(service, new object[] { executerMethodParameter });
                    }
                    else
                    {
                        //自定义类型
                        object parameter = JsonConvert.DeserializeObject(executerMethodParameter, parameterInfos[0].ParameterType);
                        serviceResult = (ServiceResult)methodInfo.Invoke(service, new object[] { parameter });
                    }

                    data = JsonConvert.SerializeObject(serviceResult.Data);

                    LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.data:" + serviceResult.Data);
                    LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.message:" + serviceResult.Message);
                    LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.pushable:" + serviceResult.Pushable);

                    string result = JsonConvert.SerializeObject(serviceResult);
                    bool isSaved = RT.StringSet(key, result, TimeSpan.FromSeconds(_ONSRedisServiceResultExpireIn));

                    if (isSaved)
                    {
                        if (serviceResult.Pushable)
                        {
                            // 本地事务成功则提交消息
                            transactionStatus = TransactionStatus.CommitTransaction;
                        }
                        else
                        {
                            // 本地事务失败则回滚消息
                            transactionStatus = TransactionStatus.RollbackTransaction;
                            failureReason = method + "执行返回serviceResult.Pushable为false，可能是该方法逻辑上返回serviceResult.Pushable为false，也可能是该方法执行时它自己捕捉到错误返回serviceResult.Pushable为false";
                        }
                    }
                }
                else
                {
                    // 不存在key则回滚消息，这里不能用RollbackTransaction，因为我们使用相同的topic在各个网站之间，这种机制会导致其他网站也会调用check方法，一旦RollbackTransaction，那么原先的网站不再重试调用check方法了，因此必须返回
                    transactionStatus = TransactionStatus.Unknow;
                    failureReason = "ExecuterMethodDictionary中不存在key:" + value.getUserProperties("executerMethod");
                    LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.error:ExecuterMethodDictionary中不存在key:" + value.getUserProperties("executerMethod"));
                }
            }
            catch (Exception e)
            {
                //exception handle
                failureReason = "捕获异常：" + e.ToString();
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.error:" + e.Message);
            }

            try
            {
                if (RT != null)
                {
                    string producedTimesKey = key + ":" + _ApplicationAlias + ":producedtimes";
                    string producedTimesValue = RT.StringGet(producedTimesKey);
                    int producedTimes = 0;

                    //写BaseMsgData数据
                    ConsumeData baseMsgData = new ConsumeData();

                    if (string.IsNullOrEmpty(producedTimesValue))
                    {
                        //不存在key，则新增
                        producedTimes++;
                        bool isSaved = RT.StringSet(producedTimesKey, producedTimes, TimeSpan.FromSeconds(_ONSRedisServiceResultExpireIn));
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

                    ProduceData produceData = new ProduceData();
                    produceData.Accomplishment = transactionStatus == TransactionStatus.CommitTransaction ? 1 : 0;
                    produceData.ApplicationAlias = _ApplicationAlias;
                    produceData.Topic = topic;
                    produceData.Tag = tag;
                    produceData.ProducerId = pid;
                    produceData.Key = key;
                    produceData.Type = ONSMessageType.TRAN.ToString();
                    produceData.Message = body;
                    produceData.Data = data;
                    produceData.TransactionType = "Executer";
                    produceData.Method = method;
                    produceData.ServiceResult = JsonConvert.SerializeObject(serviceResult);
                    produceData.TransactionStatus = transactionStatus.ToString();
                    produceData.FailureReason = failureReason;
                    produceData.ProducedTimes = producedTimes;
                    produceData.Pushable = transactionStatus == TransactionStatus.CommitTransaction ? 1 : 0;
                    produceData.ShardingKey = shardingKey;
                    NestDataHelper.WriteData(produceData);
                }
            }
            catch (Exception e)
            {
                failureReason = "捕获异常：" + e.ToString();
                LogHelper.Log(e.ToString());
            }

            return transactionStatus;
        }
    }
}
