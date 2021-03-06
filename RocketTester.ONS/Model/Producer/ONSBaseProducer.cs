﻿using System;
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

        public ONSBaseProducer(string topic, string producerId, ons.Producer producer)
        {
            this.Topic = topic;
            this.ProducerId = producerId;
            this.Type = ONSMessageType.BASE.ToString().ToUpper();
            _producer = producer;
        }

        /// <summary>
        /// 代理OrderProducer实例的start方法
        /// </summary>
        public void start()
        {
            if (_producer != null)
            {
                _producer.start();
            }
        }

        /// <summary>
        /// 代理OrderProducer实例的shutdown方法
        /// </summary>
        public void shutdown()
        {
            if (_producer != null)
            {
                _producer.shutdown();
            }
        }

        /// <summary>
        /// 代理OrderProducer实例的send方法
        /// </summary>
        /// <param name="message">Message实例</param>
        /// <param name="parameter">parameter参数</param>
        /// <returns>SendResultONS实例</returns>
        public SendResultONS send(Message message, object parameter)
        {
            SendResultONS sendResultONS = null;
            if (_producer != null)
            {
                sendResultONS = _producer.send(message);
            }
            return sendResultONS;
        }
    }
}
