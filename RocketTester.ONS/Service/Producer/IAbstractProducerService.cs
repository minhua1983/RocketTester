using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public interface IAbstractProducerService<T>
    {
        Enum TopicTag { get; }

        /// <summary>
        /// 服务的外部调用方法
        /// </summary>
        /// <param name="model">参数</param>
        /// <returns>ServiceResult实例</returns>
        ServiceResult Process(T model);
    }
}
