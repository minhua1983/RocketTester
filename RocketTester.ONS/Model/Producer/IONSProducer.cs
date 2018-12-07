using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ons;

namespace RocketTester.ONS
{
    /// <summary>
    /// 生产者代理类的接口
    /// </summary>
    public interface IONSProducer
    {
        /// <summary>
        /// 自定义属性ProducerId
        /// </summary>
        string ProducerId { get; }

        /// <summary>
        /// 自定义属性Topic
        /// </summary>
        string Topic { get; }

        /// <summary>
        /// 自定义属性Type，用来区别消息类型，BASE，ORDER，TRAN
        /// </summary>
        string Type { get; }

        /// <summary>
        /// 代理Producer实例的start方法
        /// </summary>
        void start();

        /// <summary>
        /// 代理Producer实例的shutdown方法
        /// </summary>
        void shutdown();

        /// <summary>
        /// 代理Producer实例的send方法
        /// </summary>
        /// <param name="message">Message实例</param>
        /// <param name="parameter">parameter参数</param>
        /// <returns>SendResultONS实例</returns>
        SendResultONS send(Message message, object parameter);
    }
}
