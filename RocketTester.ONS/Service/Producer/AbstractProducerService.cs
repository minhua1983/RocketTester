using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public abstract class AbstractProducerService
    {
        public Enum TopicTag { get; protected set; }
    }
}
