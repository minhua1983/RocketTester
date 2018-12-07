using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public abstract class AbstractOrderConsumerService<T> : AbstractConsumerService<T>
    {
        public AbstractOrderConsumerService(params Enum[] topicTagList)
            : base(topicTagList)
        {
        }
    }
}
