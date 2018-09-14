using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using System.Configuration;
using Newtonsoft.Json;
using ons;
using RocketTester.ONS.Util;
using Redis.Framework;

namespace RocketTester.ONS.Model
{

    public class MyLocalTransactionExecuter : LocalTransactionExecuter
    {
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        static int _ONSRedisDBNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisDBNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["ONSRedisDBNumber"]);
        static int _ONSRedisTransactionResultExpireIn = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisTransactionResultExpireIn"]) ? 18000 : int.Parse(ConfigurationManager.AppSettings["ONSRedisTransactionResultExpireIn"]);
        static bool _ONSCheckerTest = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSCheckerTest"]) ? false : bool.Parse(ConfigurationManager.AppSettings["ONSCheckerTest"].ToLower());

        /*
        Func<T, string> _func;
        T _model;

        public MyLocalTransactionExecuter(Func<T, string> fun,T model)
        {
            _func = fun;
            _model = model;
        }
        //*/


        ~MyLocalTransactionExecuter()
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
            // 如果要求消息绝对不重复，推荐做法是对消息体 body 使用 crc32或 md5来防止重复消息
            TransactionStatus transactionStatus = TransactionStatus.Unknow;
            string key = value.getKey();
            LogHelper.Log("MyLocalTransactionExecuter.execute.key  " + key);

            try
            {
                //测试Check方法用，模拟出现问题，以Unknown状态提交消息
                if (_ONSCheckerTest)
                {
                    LogHelper.Log("MyLocalTransactionExecuter -> MyLocalTransactionChecker");
                    transactionStatus = TransactionStatus.Unknow;
                    return transactionStatus;
                }

                LogHelper.Log("MyLocalTransactionExecuter.execute.after try...");
                LogHelper.Log("MyLocalTransactionExecuter.execute.executerFunc " + value.getUserProperties("executerFunc"));
                LogHelper.Log("MyLocalTransactionExecuter.execute.executerFuncModel" + value.getUserProperties("executerFuncModel"));
                //*
                //string funcResult = _func(_model);
                Func<string, TransactionResult> executerFunc = ONSHelper.ExecuterFuncDictionary[value.getUserProperties("executerFunc")];
                string executerFuncModel = value.getUserProperties("executerFuncModel");
                TransactionResult transactionResult = executerFunc(executerFuncModel);

                LogHelper.Log("MyLocalTransactionExecuter.execute.message:" + transactionResult.Message);
                LogHelper.Log("MyLocalTransactionExecuter.execute.isToPush:" + transactionResult.IsToPush);

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
                    LogHelper.Log("MyLocalTransactionExecuter.execute.result:true");
                }
                catch (Exception e)
                {
                    LogHelper.Log("MyLocalTransactionExecuter.execute.result:false, error:" + e.Message);
                }

                if (transactionResult.IsToPush)
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
            catch (Exception e)
            {
                //exception handle
                LogHelper.Log("MyLocalTransactionExecuter.execute.error:" + e.Message);
            }
            return transactionStatus;
        }
    }
}
