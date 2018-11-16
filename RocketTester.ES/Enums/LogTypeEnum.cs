using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTester.ES
{
    public enum LogTypeEnum
    {
        [Description("INFO")]
        INFO = 0,
        [Description("ERROR")]
        ERROR = 1,
        [Description("TRACE")]
        TRACE = 2,
        [Description("FATAL")]
        FATAL = 3
    }
}
