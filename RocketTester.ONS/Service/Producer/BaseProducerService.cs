using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ons;

namespace RocketTester.ONS
{
    public sealed class BaseProducerService<T> : AbstractBaseProducerService<T>
    {
        public BaseProducerService(Enum topicTag)
            : base(topicTag)
        {

        }

        public static bool SendMq(Enum topicTag, T model)
        {
            BaseProducerService<T> service = new BaseProducerService<T>(topicTag);
            return service.Process(model);
        }

        protected override bool ProcessCore(T model)
        {
            //普通消息处理逻辑写在外面

            return true;
        }
        
    }
}
