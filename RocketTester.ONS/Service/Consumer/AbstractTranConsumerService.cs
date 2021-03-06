﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public abstract class AbstractTranConsumerService<T> : AbstractConsumerService<T>
    {
        public AbstractTranConsumerService(params Enum[] topicTagList)
            : base(topicTagList)
        {
        }
    }
}
