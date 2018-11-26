using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using ons;

namespace RocketTester.ONS
{
    public class ONSTranConsumer : IONSConsumer
    {
        /// <summary>
        /// 自定义属性TagList
        /// </summary>
        public List<string> TagList { get; set; }

        /// <summary>
        /// 自定义属性Topic
        /// </summary>
        public string Topic { get; private set; }

        /// <summary>
        /// 自定义属性ConsumerId
        /// </summary>
        public string ConsumerId { get; private set; }

        /// <summary>
        /// 自定义属性Type
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// 自定义属性ClassType对应消费实例的类型
        /// </summary>
        public Type ClassType { get; private set; }

        /// <summary>
        /// 原始消费者实例
        /// </summary>
        PushConsumer _consumer;

        /// <summary>
        /// 监听者实例（即真正消费处理底层核心类）
        /// </summary>
        ONSMessageListener _listener;

        public ONSTranConsumer(string topic, string consumerId, PushConsumer consumer, Type classType)
        {
            ClassType = classType;
            Type = ONSMessageType.TRAN.ToString();
            Topic = topic;
            ConsumerId = consumerId;
            _consumer = consumer;
            TagList = new List<string>();
        }

        public void start()
        {
            if (_consumer != null)
            {
                _consumer.start();
            }
        }

        public void shutdown()
        {
            if (_consumer != null)
            {
                _consumer.shutdown();
            }
        }

        public void subscribe(string topic, string tags)
        {
            _listener = new ONSMessageListener(ClassType);
            _consumer.subscribe(topic, tags, _listener);
        }
    }
}
