using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ons;

namespace RocketTester.ONS
{
    /// <summary>
    /// 消费者代理类的接口
    /// </summary>
    public interface IONSConsumer
    {
        /// <summary>
        /// 自定义属性TagList
        /// </summary>
        List<string> TagList { get; set; }

        /// <summary>
        /// 自定义属性ConsumerId
        /// </summary>
        string ConsumerId { get; }

        /// <summary>
        /// 自定义属性Topic
        /// </summary>
        string Topic { get; }

        /// <summary>
        /// 自定义属性Type，用来区别消息类型，BASE，ORDER，TRAN
        /// </summary>
        string Type { get; }

        /// <summary>
        /// 自定义属性ClassType对应消费实例的类型
        /// </summary>
        Type ClassType { get; }

        /// <summary>
        /// 代理Producer实例的start方法
        /// </summary>
        void start();

        /// <summary>
        /// 代理Producer实例的shutdown方法
        /// </summary>
        void shutdown();

        /// <summary>
        /// 订阅某种topic下的tag列表
        /// </summary>
        /// <param name="topic">topic</param>
        /// <param name="tags">标签列表，以||分割</param>
        void subscribe(string topic, string tags);
    }
}
