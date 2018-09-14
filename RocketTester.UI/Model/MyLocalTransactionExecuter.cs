using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ons;

namespace RocketTester.UI.Model
{
    public class MyLocalTransactionExecuter : LocalTransactionExecuter
    {
        public MyLocalTransactionExecuter()
        {
        }

        ~MyLocalTransactionExecuter()
        {
        }

        public override TransactionStatus execute(Message value)
        {
            Console.WriteLine("execute topic: {0}, tag:{1}, key:{2}, msgId:{3},msgbody:{4}, userProperty:{5}",
            value.getTopic(), value.getTag(), value.getKey(), value.getMsgID(), value.getBody(), value.getUserProperties("VincentNoUser"));
            // 消息 ID(有可能消息体一样，但消息 ID 不一样。当前消息 ID 在控制台无法查询)
            string msgId = value.getMsgID();
            // 消息体内容进行 crc32, 也可以使用其它的如 MD5
            // 消息 ID 和 crc32id 主要是用来防止消息重复
            // 如果要求消息绝对不重复，推荐做法是对消息体 body 使用 crc32或 md5来防止重复消息
            TransactionStatus transactionStatus = TransactionStatus.Unknow;
            try
            {
                bool isCommit = true;
                if (isCommit)
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
            }
            return transactionStatus;
        }
    }
}
