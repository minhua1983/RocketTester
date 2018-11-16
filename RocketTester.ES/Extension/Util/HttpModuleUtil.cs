using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using System.Web;

[assembly: PreApplicationStartMethod(typeof(RocketTester.ES.HttpModuleUtil), "LoadPre")]
namespace RocketTester.ES
{
    public class HttpModuleUtil
    {
        static bool _hasLoad;
        static object _lockHeler = new object();
        static string _AutoRegisterGeneralHttpModule = ConfigurationManager.AppSettings["AutoRegisterGeneralHttpModule"] ?? "0";

        public static void LoadPre()
        {
            if (_AutoRegisterGeneralHttpModule == "1")
            {
                if (!_hasLoad)
                {
                    lock (_lockHeler)
                    {
                        if (!_hasLoad)
                        {
                            _hasLoad = true;

                            //命名空间    Microsoft.Web.Infrastructure.DynamicModuleHelper;

                            DynamicModuleUtility.RegisterModule(typeof(GeneralHttpModule));
                        }
                    }
                }
            }
        }
    }
}
