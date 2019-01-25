using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using ons;

namespace RocketTester.ONS
{
    /// <summary>
    /// 事务生产者类不支持自定义属性，继承它又需要自己实现它的构造，因此无法继承他来扩展，但又不能修改它类的定义，因此这里使用代理模式类来扩展它。
    /// </summary>
    public class ONSTranProducer : IONSProducer
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

        TransactionProducer _producer;

        public ONSTranProducer(string topic, string producerId, TransactionProducer transactionProducer)
        {
            this.Topic = topic;
            this.ProducerId = producerId;
            this.Type = ONSMessageType.TRAN.ToString().ToUpper(); ;
            _producer = transactionProducer;
        }

        /// <summary>
        /// 代理TransactionProducer实例的start方法
        /// </summary>
        public void start()
        {
            if (_producer != null)
            {
                _producer.start();
            }
        }

        /// <summary>
        /// 代理TransactionProducer实例的shutdown方法
        /// </summary>
        public void shutdown()
        {
            if (_producer != null)
            {
                _producer.start();
            }
        }

        /// <summary>
        /// 代理TransactionProducer实例的send方法
        /// </summary>
        /// <param name="message">Message实例</param>
        /// <param name="parameter">parameter参数</param>
        /// <returns>SendResultONS实例</returns>
        public SendResultONS send(Message message, object parameter)
        {
            SendResultONS sendResultONS = null;
            if (_producer != null)
            {
                ONSLocalTransactionExecuter executer = parameter as ONSLocalTransactionExecuter;
                sendResultONS = _producer.send(message, executer);
            }
            return sendResultONS;
        }
    }
}
