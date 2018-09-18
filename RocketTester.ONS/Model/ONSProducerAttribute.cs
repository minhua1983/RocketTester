using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RocketTester.ONS.Enum;

namespace RocketTester.ONS.Model
{
    /// <summary>
    /// 上游生产者特性
    /// </summary>
    public class ONSProducerAttribute : Attribute
    {
        /// <summary>
        /// 消息的主题
        /// </summary>
        public ONSMessageTopic Topic { get; set; }

        /// <summary>
        /// 消息的标签
        /// </summary>
        public ONSMessageTag Tag { get; set; }

        public ONSProducerAttribute(ONSMessageTopic topic, ONSMessageTag tag)
        {
            Topic = topic;
            Tag = tag;
        }
    }
}