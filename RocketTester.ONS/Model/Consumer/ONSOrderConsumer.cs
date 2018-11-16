using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using ons;

namespace RocketTester.ONS
{
    public class ONSOrderConsumer : IONSConsumer
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

        OrderConsumer _consumer;

        public ONSOrderConsumer(string topic, string consumerId, OrderConsumer consumer, Type classType)
        {
            ClassType = classType;
            Type = ONSMessageType.ORDER.ToString();
            Topic = topic;
            ConsumerId = consumerId;
            _consumer = consumer;
            TagList = new List<string>();
        }

        public void start()
        {
            _consumer.start();
        }

        public void shutdown()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            _consumer.shutdown();
            stopwatch.Stop();
            DebugUtil.Debug("ONSOrderConsumer spent " + stopwatch.ElapsedMilliseconds + " on shutdown.");
        }

        public void subscribe(string topic, string tags)
        {
            ONSMessageOrderListener listener = new ONSMessageOrderListener(ClassType);
            _consumer.subscribe(topic, tags, listener);
        }
    }
}
