using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public enum BASE_COMMON
    {
        /// <summary>
        /// 发送普通邮件，会有一定限制条件，如按每个相同的主题每10秒只能发送一次
        /// </summary>
        SEND_MAIL,

        /// <summary>
        /// 调用JPUSH来推送信息给用户的客户端
        /// </summary>
        CALL_JPUSH,

        /// <summary>
        /// 测试主题（每次测试各种功能都用这个主题）
        /// </summary>
        TEST,

        /// <summary>
        /// 测试主题（用于测试消费时再次发送消息）
        /// </summary>
        TEST2,
    }
}
