using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public abstract class AbstractConsumerService<T> : IAbstractConsumerService
    {
        public Enum[] TopicTagList { get; private set; }

        public AbstractConsumerService(params Enum[] topicTagList)
        {
            TopicTagList = topicTagList;
        }

        /// <summary>
        /// Consume抽象方法，主要用于派生类重写它逻辑，即下游消费者的消费方法。此消费方法务必实现逻辑上的幂等，因为目前看aliyun的rocketmq有不少重复消费的现象，为了确保数据的一致性，此方法必须实现幂等，即调用一百次和调用一次的效果是一致的。
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
