using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ONS
{
    public enum BASE_TMS
    {
        /// <summary>
        /// 创建运单
        /// </summary>
        CREATE_WAYBILL,

        /// <summary>
        /// 删除预订单
        /// </summary>
        DELETE_PREWAYBILL,

        /// <summary>
        /// 原始采购单
        /// </summary>
        ORIGINAL_PURCHASE_ORDER,
    }
}
