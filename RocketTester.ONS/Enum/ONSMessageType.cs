using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    /// <summary>
    /// 消息种类，此枚举中的值必须由框架维护，它的值必须全大写
    /// </summary>
    public enum ONSMessageType
    {
        BASE,
        ORDER,
        TRAN
    }
}
