﻿using System;
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
            OrderAction action = ons.OrderAction.Suspend;

            try
            {
                //DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",consume...");

                bool needToCommit = ListenerHelper.React(value, this.ClassType);

                if (needToCommit)
                {
                    //DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ons.OrderAction.Success...\n");
                    action = ons.OrderAction.Success;
                }
                else
                {
                    //DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",ons.OrderAction.Suspend;...\n");
                    action = ons.OrderAction.Suspend;
                }
            }
            catch (Exception e)
            {
                DebugUtil.Debug("MESSAGE_KEY:" + value.getKey() + ",error:" + e.ToString());
            }

            return action;
        }
    }
}
