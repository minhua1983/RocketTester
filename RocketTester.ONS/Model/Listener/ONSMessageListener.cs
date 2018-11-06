using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Reflection;
using Action = ons.Action;
using ons;
using Redis.Framework;
using Newtonsoft.Json;
using Nest.Framework;

namespace RocketTester.ONS
{
    /// <summary>
    /// MyMessageListener类继承自ons.MessageListener。当pushConsumer订阅消息后，会调用consume。同一个网站下不允许不同的ConsumerService使用相同的Topic，且Tag也相同
    /// </summary>
    public class ONSMessageListener : MessageListener
    {
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        static int _AliyunOnsRedisDbNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["AliyunOnsRedisDbNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["AliyunOnsRedisDbNumber"]);
        static int _AliyunOnsRedisServiceResultExpireIn = string.IsNullOrEmpty(ConfigurationManager.AppSettings["AliyunOnsRedisServiceResultExpireIn"]) ? 86400 : int.Parse(ConfigurationManager.AppSettings["AliyunOnsRedisServiceResultExpireIn"]);
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

            DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",consume...");

            bool needToCommit = ListenerHelper.React(value);

            Action action;
            if (needToCommit)
            {
                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ons.Action.CommitMessage...\n");
                action = ons.Action.CommitMessage;
            }
            else
            {
                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ons.Action.ReconsumeLater...\n");
                action = ons.Action.ReconsumeLater;
            }

            return action;
        }
    }
}
