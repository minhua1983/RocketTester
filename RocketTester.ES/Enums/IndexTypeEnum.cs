using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ES
{
    /// <summary>
    /// IndexTypeEnum枚举，此枚举用来约束indexType（indexType即index的一部分{environment}.{indexType}.{yyyy.MM.dd}，如d.demo.2018.09.12）。
    /// 通常日志类型数据的{environment}.{indexType}都是由web.config中的EXIndex来指定的，但是数据类型的数据需要用EXIndex中{environment}+此枚举值{indexType}+日期{yyyy.MM.dd}组成
    /// </summary>
    public enum IndexTypeEnum
    {
        /// <summary>
        /// 测试数据
        /// </summary>
        DEMO,

        /// <summary>
        /// jdy 的数据统计
        /// </summary>
        JDY_Statics,

        /// <summary>
        /// 上游生产者的日志数据
        /// </summary>
        PRODUCER_DATA,

        /// <summary>
        /// 下游消费者的日志数据
        /// </summary>
        CONSUMER_DATA,
    }
}
