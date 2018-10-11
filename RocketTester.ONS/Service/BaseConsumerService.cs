using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RocketTester.ONS.Enum;
using RocketTester.ONS.Model;

namespace RocketTester.ONS.Service
{
    public abstract class BaseConsumerService<T>
    {
        public ONSMessageTopic Topic { get; private set; }
        public ONSMessageTag Tag { get; private set; }

        public BaseConsumerService(ONSMessageTopic topic, ONSMessageTag tag)
        {
            Topic = topic;
            Tag = tag;
        }

        /// <summary>
        /// Consume抽象方法，主要用于派生类重写它逻辑，即下游消费者的消费方法。
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>是否消费成功</returns>
        protected abstract bool ProcessCore(T model);

        /// <summary>
        /// 通过反射调用
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>是否消费成功</returns>
        protected bool InternalProcess(T model)
        {
            //此处预留可以做干预
            bool result = ProcessCore(model);
            //此处预留可以做干预
            return result;
        }
    }
}
