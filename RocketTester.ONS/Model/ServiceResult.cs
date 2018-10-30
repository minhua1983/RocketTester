using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    /// <summary>
    /// 事务结果类型
    /// </summary>
    public class ServiceResult
    {
        /// <summary>
        /// 是否需要把消息推送到消息中心
        /// </summary>
        public bool Pushable { get; set; }

        /// <summary>
        /// 信息（取消推送的话，可以把取消推送的原因写在这里；确认要需要推送的话，可以把成功的信息写在这里）
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 要传递什么数据给到下游订阅者（这个数据的格式需要和下游订阅者协商，由于上下游可能不是一个开发语言开发的，因此建议使用json字符把当前对象的实例传递给到下游）
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// 需要传递Producer实例的参数，例如顺序消息在send时需要传递shardingKey就需要通过这个参数传递，定时消息需要通过这个参数传递发送时间
        /// </summary>
        public string Parameter { get; set; }

        public ServiceResult()
        {
            Pushable = true;
        }
    }
}
