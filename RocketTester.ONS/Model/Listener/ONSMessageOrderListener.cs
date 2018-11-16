using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ons;
using Action = ons.Action;

namespace RocketTester.ONS
{
    /// <summary>
    /// MyMessageListener类继承自ons.MessageListener。当pushConsumer订阅消息后，会调用consume。同一个网站下不允许不同的ConsumerService使用相同的Topic，且Tag也相同
    /// </summary>
    public class ONSMessageOrderListener : MessageOrderListener
    {
        public Type ClassType { get; private set; }

        public ONSMessageOrderListener(Type type)
        {
            this.ClassType = type;
        }

        ~ONSMessageOrderListener()
        {
        }

        public override OrderAction consume(Message value, ConsumeOrderContext context)
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

            bool needToCommit = ListenerHelper.React(value, this.ClassType);

            OrderAction action;

            if (needToCommit)
            {
                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ons.OrderAction.Success...\n");
                action = ons.OrderAction.Success;
            }
            else
            {
                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ons.OrderAction.Suspend;...\n");
                action = ons.OrderAction.Suspend;
            }

            return action;
        }
    }
}
