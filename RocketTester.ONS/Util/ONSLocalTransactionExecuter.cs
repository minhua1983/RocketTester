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
using RocketTester.ONS.Model;
using RocketTester.ONS.Service;

namespace RocketTester.ONS.Util
{

    public class ONSLocalTransactionExecuter : LocalTransactionExecuter
    {
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        static int _ONSRedisDBNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisDBNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["ONSRedisDBNumber"]);
        static int _ONSRedisTransactionResultExpireIn = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisTransactionResultExpireIn"]) ? 18000 : int.Parse(ConfigurationManager.AppSettings["ONSRedisTransactionResultExpireIn"]);

        /*
        Func<T, string> _func;
        T _model;

        public ONSLocalTransactionExecuter(Func<T, string> fun,T model)
        {
            _func = fun;
            _model = model;
        }
        //*/


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
            TransactionStatus transactionStatus = TransactionStatus.Unknow;
            string key = value.getKey();
            LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.key  " + key);

            try
            {
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.after try...");
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.executerMethod " + value.getUserProperties("executerMethod"));
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.executerMethodParameter " + value.getUserProperties("executerMethodParameter"));

                if (ONSHelper.ExecuterMethodDictionary.ContainsKey(value.getUserProperties("executerMethod")))
                {
                    MethodInfo methodInfo = ONSHelper.ExecuterMethodDictionary[value.getUserProperties("executerMethod")];
                    string data = value.getUserProperties("executerMethodParameter");
                    Type type = methodInfo.ReflectedType;

                    Assembly assembly = Assembly.GetAssembly(type);
                    object service = assembly.CreateInstance(type.FullName);
                    ParameterInfo[] parameterInfos = methodInfo.GetParameters();

                    LogHelper.Log(parameterInfos[0].ParameterType.ToString());


                    ONSTransactionResult transactionResult;

                    //判断类型
                    if (parameterInfos[0].ParameterType.ToString().ToLower() == "system.string")
                    {
                        //string类型
                        transactionResult = (ONSTransactionResult)methodInfo.Invoke(service, new object[] { data });
                    }
                    else
                    {
                        //自定义类型
                        object parameter = JsonConvert.DeserializeObject(data, parameterInfos[0].ParameterType);

                        /*
                        LogHelper.Log(service.GetType().FullName);
                        LogHelper.Log(service.GetType().GetProperty("Tag").GetValue(service).ToString());
                        LogHelper.Log(service.GetType().GetProperty("Topic").GetValue(service).ToString());
                        LogHelper.Log(parameterInfos[0].ParameterType.ToString());
                        LogHelper.Log(methodInfo.Name);
                        LogHelper.Log(methodInfo.DeclaringType.FullName);
                        LogHelper.Log(methodInfo.ReflectedType.FullName);

                        MethodInfo mi = service.GetType().GetMethod("Test", BindingFlags.NonPublic | BindingFlags.Instance);
                        //mi.Invoke(service, new object[] { parameter });
                        methodInfo.Invoke(service, new object[] { parameter });
                        transactionResult = new ONSTransactionResult();
                        //*/

                        transactionResult = (ONSTransactionResult)methodInfo.Invoke(service, new object[] { parameter });

                        LogHelper.Log("..................");
                        
                    }


                    LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.data:" + transactionResult.Data);
                    LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.message:" + transactionResult.Message);
                    LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.pushable:" + transactionResult.Pushable);

                    string result = JsonConvert.SerializeObject(transactionResult);

                    try
                    {
                        RedisTool RT = new RedisTool(_ONSRedisDBNumber, _RedisExchangeHosts);
                        bool isSaved = RT.StringSet(key, result, TimeSpan.FromSeconds(_ONSRedisTransactionResultExpireIn));
                        if (!isSaved)
                        {
                            transactionStatus = TransactionStatus.Unknow;
                            return transactionStatus;
                        }
                        LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.result:true");
                    }
                    catch (Exception e)
                    {
                        LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.result:false, error:" + e.Message);
                    }

                    if (transactionResult.Pushable)
                    {
                        // 本地事务成功则提交消息
                        transactionStatus = TransactionStatus.CommitTransaction;
                    }
                    else
                    {
                        // 本地事务失败则回滚消息
                        transactionStatus = TransactionStatus.RollbackTransaction;
                    }
                }
                else
                {
                    // 不存在key则回滚消息，这里不能用RollbackTransaction，因为我们使用相同的topic在各个网站之间，这种机制会导致其他网站也会调用check方法，一旦RollbackTransaction，那么原先的网站不再重试调用check方法了，因此必须返回
                    transactionStatus = TransactionStatus.Unknow;
                    LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.error:ExecuterMethodDictionary中不存在key:" + value.getUserProperties("executerMethod"));
                }
            }
            catch (Exception e)
            {
                //exception handle
                LogHelper.Log("MESSAGE_KEY:" + value.getKey() + ",ONSLocalTransactionExecuter.execute.error:" + e.Message);
            }
            return transactionStatus;
        }
    }
}
