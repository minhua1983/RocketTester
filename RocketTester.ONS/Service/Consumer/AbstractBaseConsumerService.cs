using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public abstract class AbstractBaseConsumerService<T> : AbstractConsumerService<T>
    {
        public AbstractBaseConsumerService(params Enum[] topicTagList)
            : base(topicTagList)
        {
        }
    }
}
