using System;
using System.Web;
using System.Net;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Nest.Framework;
using Liinji.Common;

namespace RocketTester.ES
{
    public class GeneralHttpModule : IHttpModule
    {
        /// <summary>
        /// You will need to configure this module in the Web.config file of your
        /// web and register it with IIS before being able to use it. For more information
        /// see the following link: http://go.microsoft.com/?linkid=8101007
        /// </summary>
        #region IHttpModule Members

        public void Dispose()
        {
            //clean-up code here.
        }

        public void Init(HttpApplication context)
        {

            // Below is an example of how you can handle LogRequest event and provide 
            // custom logging implementation for it

            context.BeginRequest += (o, e) =>
            {
                //定义Response.Filter，这样在EndRequest中可以用((ResponseFilter)Response.Filter).Body获取到响应的内容
                HttpContext.Current.Response.Filter = new ResponseFilter(context.Response.Filter);

                //开始追踪
                GeneralLogUtil.BeginTrace();
            };

            context.EndRequest += (o, e) =>
            {
                //结束追踪并将追踪日志写入es
                GeneralLogUtil.EndTrace();
            };

            context.Error += (o, e) =>
            {
                if (HttpContext.Current != null)
                {
                    Exception lastError = HttpContext.Current.Server.GetLastError();

                    string logId = GeneralLogUtil.Error(lastError.ToString());

                    ServiceReturnMsg serviceReturnMsg = new ServiceReturnMsg()
                    {
                        ReturnCode = -9999,
                        ReturnMsg = "服务器异常:" + logId
                    };
                    string json = JsonConvert.SerializeObject(serviceReturnMsg);
                    HttpContext.Current.Response.Write(json);
                    HttpContext.Current.Response.End();


                }
            };
        }



        #endregion
    }
}
