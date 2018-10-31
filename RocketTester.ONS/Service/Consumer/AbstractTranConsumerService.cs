using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RocketTester.ONS
{
    public abstract class AbstractTranConsumerService<T> : AbstractConsumerService<T>, IAbstractConsumerService
    {
        public Enum[] TopicTagList { get; private set; }

        public AbstractTranConsumerService(params Enum[] topicTagList)
        {
            TopicTagList = topicTagList;
        }
    }
}
