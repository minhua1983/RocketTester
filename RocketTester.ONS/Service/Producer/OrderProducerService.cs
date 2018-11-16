using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public sealed class OrderProducerService<T> : AbstractOrderProducerService<T>
    {
        public OrderProducerService(Enum topicTag)
            : base(topicTag)
        {

        }

        public static bool SendMq(Enum topicTag, string shardingKey, T model)
        {
            OrderProducerService<T> service = new OrderProducerService<T>(topicTag);
            return service.Process(model, shardingKey);
        }

        protected override bool ProcessCore(T model)
        {
            //顺序消息处理逻辑写在外面

            return true;
        }
    }
}
