using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public abstract class AbstractConsumerService
    {
        public Enum[] TopicTagList { get; protected set; }
    }
}
