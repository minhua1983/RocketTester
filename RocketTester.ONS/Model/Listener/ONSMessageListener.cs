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
        public Type ClassType { get; private set; }

        public ONSMessageListener(Type type)
        {
            this.ClassType = type;
        }

        ~ONSMessageListener()
        {
        }

        public override Action consume(Message value, ConsumeContext context)
        {
            Action action = ons.Action.ReconsumeLater;
            
            try
            {
                //DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",consume...");

                bool needToCommit = ListenerHelper.React(value, this.ClassType);

                if (needToCommit)
                {
                    //DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ons.Action.CommitMessage...\n");
                    action = ons.Action.CommitMessage;
                }
                else
                {
                    //DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ons.Action.ReconsumeLater...\n");
                    action = ons.Action.ReconsumeLater;
                }
            }
            catch (Exception e)
            {
                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",error:" + e.ToString());
            }
            return action;

            return ons.Action.CommitMessage;
        }
    }
}
