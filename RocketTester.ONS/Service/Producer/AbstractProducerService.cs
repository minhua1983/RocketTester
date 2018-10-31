using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public abstract class AbstractProducerService<T>
    {
        /// <summary>
        /// ProcessCore抽象方法，主要用于派生类重写它逻辑，业务方法，需要开发人员自己实现里面的业务逻辑。
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>事务执行结果</returns>
        protected abstract ServiceResult ProcessCore(T model);

        /// <summary>
        /// 通过反射调用
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>业务执行结果</returns>
        protected ServiceResult InternalProcess(T model)
        {
            //此处预留可以做干预
            ServiceResult result = ProcessCore(model);
            //此处预留可以做干预
            return result;
        }

        /// <summary>
        /// Process抽象方法，禁止子类覆盖，它是服务类的外部调用的唯一方法
        /// </summary>
        /// <param name="model">参数</param>
        /// <returns>ServiceResult实例</returns>
        public abstract ServiceResult Process(T model);
    }
}
