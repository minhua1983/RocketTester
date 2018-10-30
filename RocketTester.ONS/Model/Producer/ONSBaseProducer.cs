using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using ons;

namespace RocketTester.ONS
{
    public class ONSBaseProducer : IONSProducer
    {
        /// <summary>
        /// 自定义属性Topic
        /// </summary>
        public string Topic { get; private set; }

        /// <summary>
        /// 自定义属性ProducerId
        /// </summary>
        public string ProducerId { get; private set; }

        /// <summary>
        /// 自定义属性Type
        /// </summary>
        public string Type { get; private set; }

        ons.Producer _producer;

        public ONSBaseProducer(string topic, string produceId, ons.Producer producer)
        {
            this.Topic = topic;
            this.ProducerId = produceId;
            this.Type = ONSMessageType.BASE.ToString().ToUpper();
            _producer = producer;
        }

        /// <summary>
        /// 代理OrderProducer实例的start方法
        /// </summary>
        public void start()
        {
            _producer.start();
        }

        /// <summary>
        /// 代理OrderProducer实例的shutdown方法
        /// </summary>
        public void shutdown()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            _producer.shutdown();
            stopwatch.Stop();
            LogHelper.Log("ONSProducer spent " + stopwatch.ElapsedMilliseconds + " on shutdown.");
        }

        /// <summary>
        /// 代理OrderProducer实例的send方法
        /// </summary>
        /// <param name="message">Message实例</param>
        /// <param name="parameter">parameter参数</param>
        /// <returns>SendResultONS实例</returns>
        public SendResultONS send(Message message, object parameter)
        {
            return _producer.send(message);
        }
    }
}
