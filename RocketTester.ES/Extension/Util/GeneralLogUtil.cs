using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using System.IO;
using Newtonsoft.Json;
using Nest.Framework;
using AutoMapper;

namespace RocketTester.ES
{
    public class GeneralLogUtil
    {
        public static void BeginTrace()
        {
            //获取请求体内容
            string requestBody = "";
            if (HttpContext.Current.Request.HttpMethod == "POST" || HttpContext.Current.Request.HttpMethod == "PUT")
            {
                //下面那句Stream stream = HttpContext.Current.Request.InputStream;千万不能加上using(){}，不然会自动释放InputStream对象，后续进入mvc流程后无法自动转换成实体了
                Stream stream = HttpContext.Current.Request.InputStream;
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                requestBody = HttpUtility.UrlDecode(Encoding.UTF8.GetString(bytes));
                HttpContext.Current.Request.InsertEntityBody(bytes, 0, bytes.Length);
                HttpContext.Current.Request.InputStream.Position = 0;
            }

            //生成日志对象
            GeneralLog generalLog = new GeneralLog();
            //generalLog.BeginEndPeriod = 0;
            generalLog.BeginTime = DateTime.Now;
            //generalLog.CreateTime= DateTime.Now;
            //generalLog.EndResponsePeriod = 0;
            //generalLog.EndTime = DateTime.Now;
            generalLog.LogMessage = "";
            generalLog.LogType = LogTypeEnum.TRACE.ToString();
            //generalLog.RequestAction = action == null ? "" : action.ToString();
            generalLog.RequestBaseUrl = HttpContext.Current.Request.Path;
            //generalLog.RequestBeginPeriod = 0;
            generalLog.RequestBody = requestBody;
            generalLog.RequestClient = HttpContext.Current.Request.Headers["Client"] ?? "";
            generalLog.RequestClientIp = HttpContext.Current.Request.UserHostAddress;
            generalLog.RequestContentType = HttpContext.Current.Request.ContentType;
            //generalLog.RequestController = controller == null ? "" : controller.ToString();
            generalLog.RequestImei = HttpContext.Current.Request.Headers["IMEI"] ?? "";
            generalLog.RequestMethod = HttpContext.Current.Request.HttpMethod;
            generalLog.RequestOpenId = HttpContext.Current.Request.Headers["OpenId"] ?? "";
            generalLog.RequestQueryString = HttpContext.Current.Request.QueryString == null ? "" : HttpContext.Current.Request.QueryString.ToString();
            generalLog.RequestRawUrl = HttpContext.Current.Request.RawUrl;
            generalLog.RequestReferer = HttpContext.Current.Request.UrlReferrer == null ? "" : HttpContext.Current.Request.UrlReferrer.ToString();
            generalLog.RequestServerIp = GetIpAddress();
            generalLog.RequestServerName = HttpContext.Current.Request.UserHostName;
            generalLog.RequestTime = generalLog.BeginTime;
            generalLog.RequestToken = HttpContext.Current.Request.Headers["Token"] ?? "";
            generalLog.RequestTraceId = HttpContext.Current.Request.Headers["TraceId"] ?? generalLog.Id;
            generalLog.RequestUrl = HttpContext.Current.Request.Url.ToString();
            generalLog.RequestUserAgent = HttpContext.Current.Request.UserAgent;
            generalLog.RequestUserId = HttpContext.Current.Request.Headers["UserId"] ?? "";
            generalLog.RequestVersion = HttpContext.Current.Request.Headers["Version"] ?? "";
            //generalLog.ResponseBody = "";
            generalLog.ResponseStatus = HttpContext.Current.Response.StatusCode;
            //generalLog.ResponseTime = DateTime.Now;

            //在当前请求中持久化日志对象
            HttpContext.Current.Items.Add("GeneralLog", generalLog);

            //在当前请求中持久化TraceId
            HttpContext.Current.Items.Add("TraceId", generalLog.RequestTraceId);
        }

        public static void EndTrace()
        {
            //判断日志对象是否存在
            if (HttpContext.Current.Items.Contains("GeneralLog"))
            {
                //尝试获取当前请求的响应内容
                StringBuilder responseBody = new StringBuilder();
                try
                {
                    ResponseFilter responseFilter = (ResponseFilter)HttpContext.Current.Response.Filter;
                    responseBody = responseFilter.Body;
                }
                catch (Exception ex)
                {
                    //本地用vs测试时会报出异常，无法将类型为“Microsoft.VisualStudio.Web.PageInspector.Runtime.Tracing.ArteryFilter”的对象强制转换为类型“RocketTester.ES.Response.Filter”
                    responseBody.Append(ex.Message);
                }

                object controller = HttpContext.Current.Request.RequestContext.RouteData.Values["controller"];
                object action = HttpContext.Current.Request.RequestContext.RouteData.Values["action"];

                //尝试从当前请求中获取之前持久化的日志对象，并更新相关属性
                GeneralLog generalLog = HttpContext.Current.Items["GeneralLog"] as GeneralLog;
                generalLog.RequestAction = action == null ? "" : action.ToString();
                generalLog.RequestController = controller == null ? "" : controller.ToString();
                generalLog.ResponseBody = responseBody == null ? "" : responseBody.ToString();
                generalLog.CreateTime = DateTime.Now;
                generalLog.EndTime = generalLog.CreateTime;
                generalLog.ResponseTime = generalLog.EndTime;
                generalLog.BeginEndPeriod = (double)((generalLog.EndTime - generalLog.BeginTime).Milliseconds) / 1000;
                generalLog.RequestBeginPeriod = (double)((generalLog.BeginTime - generalLog.RequestTime).Milliseconds) / 1000;
                generalLog.EndResponsePeriod = (double)((generalLog.ResponseTime - generalLog.EndTime).Milliseconds) / 1000;

                //写入es
                NestDataHelper.WriteLog(generalLog);
            }
        }

        public static string Info(string logMessage)
        {
            return SaveLog(LogTypeEnum.INFO, logMessage);
        }

        public static string Error(string logMessage)
        {
            return SaveLog(LogTypeEnum.ERROR, logMessage);
        }

        public static string Fatal(string logMessage)
        {
            return SaveLog(LogTypeEnum.FATAL, logMessage);
        }

        static string SaveLog(LogTypeEnum logTypeEnum, string logMessage)
        {
            if (HttpContext.Current.Items.Contains("GeneralLog"))
            {
                object controller = HttpContext.Current.Request.RequestContext.RouteData.Values["controller"];
                object action = HttpContext.Current.Request.RequestContext.RouteData.Values["action"];

                GeneralLog generalLog = HttpContext.Current.Items["GeneralLog"] as GeneralLog;
                GeneralLog errorGeneralLog = Mapper.Map<GeneralLog, GeneralLog>(generalLog);
                errorGeneralLog.Id = Guid.NewGuid().ToString();
                errorGeneralLog.LogType = logTypeEnum.ToString();
                errorGeneralLog.RequestTraceId = generalLog.Id;
                errorGeneralLog.RequestAction = action == null ? "" : action.ToString();
                errorGeneralLog.RequestController = controller == null ? "" : controller.ToString();
                errorGeneralLog.CreateTime = errorGeneralLog.CreateTime == default(DateTime) ? DateTime.Now : errorGeneralLog.CreateTime;

                //写入es
                NestDataHelper.WriteLog(generalLog);

                return errorGeneralLog.Id;
            }
            return null;
        }

        static string GetIpAddress()
        {
            string hostName = Dns.GetHostName();   //获取本机名
            IPHostEntry localhost = Dns.GetHostByName(hostName);    //方法已过期，可以获取IPv4的地址
            //IPHostEntry localhost = Dns.GetHostEntry(hostName);   //获取IPv6地址
            IPAddress localaddr = localhost.AddressList[0];

            return localaddr.ToString();
        }
    }
}
