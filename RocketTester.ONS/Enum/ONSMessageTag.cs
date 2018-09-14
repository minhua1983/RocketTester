using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS.Enum
{
    /// <summary>
    /// 消息标签的枚举，此枚举中的值必须由框架维护
    /// </summary>
    public enum ONSMessageTag
    {
        /// <summary>
        /// 用户成功注册
        /// </summary>
        UserRegistered,

        /// <summary>
        /// 用户成功预订
        /// </summary>
        UserReserved,

        /// <summary>
        /// 用户资料同步
        /// </summary>
        UserSynchronized
    }
}
