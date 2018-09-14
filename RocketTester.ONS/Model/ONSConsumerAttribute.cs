using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RocketTester.ONS.Enum;

namespace RocketTester.ONS.Model
{
    public class ONSConsumerAttribute : Attribute
    {
        public string Topic { get; set; }
        public ONSMessageTag Tag { get; set; }

        public ONSConsumerAttribute(string topic, ONSMessageTag tag)
        {
            Topic = topic;
            Tag = tag;
        }
    }
}
