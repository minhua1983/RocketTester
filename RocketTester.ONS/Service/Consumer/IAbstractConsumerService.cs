using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RocketTester.ONS.Enum;
using RocketTester.ONS.Model;

namespace RocketTester.ONS.Service
{
    public interface IAbstractConsumerService
    {
        /*
        /// <summary>
        /// 服务对应的消息主题
        /// </summary>
        ONSMessageTopic Topic { get; }

        /// <summary>
        /// 服务对应的消息标签
        /// </summary>
        ONSMessageTag Tag { get; }
        //*/

        List<TopicTag> TopicTagList { get; }
    }
}
