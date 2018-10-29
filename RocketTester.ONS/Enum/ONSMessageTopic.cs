using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS.Enum
{
    /// <summary>
    /// 事务消息的主题，此枚举中的值必须由框架维护，它的值必须全大写
    /// </summary>
    public enum ONSMessageTopic
    {
        /// <summary>
        /// tester1的事务消息通道
        /// </summary>
        TRAN_TESTER1,

        /// <summary>
        /// tester1的顺序消息通道
        /// </summary>
        ORDER_TESTER1,

        /// <summary>
        /// tester1的普通消息通道
        /// </summary>
        BASE_TESTER1,
    }
}
