using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RocketTester.ONS.Enum;

namespace RocketTester.ONS.Model
{
    /// <summary>
    /// 下游消费者特性
    /// </summary>
    public class ONSConsumerAttribute : Attribute
    {
        /// <summary>
        /// 消息的主题
        /// </summary>
        public ONSMessageTopic Topic { get; set; }

        /// <summary>
        /// 消息的标签
        /// </summary>
        public ONSMessageTag Tag { get; set; }

        public ONSConsumerAttribute(ONSMessageTopic topic, ONSMessageTag tag)
        {
            Topic = topic;
            Tag = tag;
        }
    }
}
