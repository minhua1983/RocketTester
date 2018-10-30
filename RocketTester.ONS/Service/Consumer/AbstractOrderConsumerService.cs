﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RocketTester.ONS
{
    public abstract class AbstractOrderConsumerService<T> : IAbstractConsumerService
    {
        public Enum[] TopicTagList { get; private set; }

        public AbstractOrderConsumerService(params Enum[] topicTagList)
        {
            TopicTagList = topicTagList;
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
