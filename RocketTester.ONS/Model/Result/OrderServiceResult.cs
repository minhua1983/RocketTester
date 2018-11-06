using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    /// <summary>
    /// 顺序消息服务结果类型
    /// </summary>
    public class OrderServiceResult : ServiceResult
    {
        /// <summary>
        /// 需要传递Producer实例的参数，例如顺序消息在send时需要传递ShardingKey就需要通过这个参数传递，定时消息需要通过这个参数传递发送时间
        /// </summary>
        public string ShardingKey { get; set; }
    }
}
