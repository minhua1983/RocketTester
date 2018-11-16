using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nest.Framework;

namespace RocketTester.ES
{
    public class GeneralLog : AbstractModel
    {
        [JsonProperty("beginEndPeriod")]
        public double BeginEndPeriod { get; set; }

        [JsonProperty("beginTime")]
        public DateTime BeginTime { get; set; }

        [JsonProperty("createTime")]
        public DateTime CreateTime { get; set; }

        [JsonProperty("endResponsePeriod")]
        public double EndResponsePeriod { get; set; }

        [JsonProperty("endTime")]
        public DateTime EndTime { get; set; }

        [JsonProperty("logMessage")]
        public string LogMessage { get; set; }

        [JsonProperty("logType")]
        public string LogType { get; set; }

        [JsonProperty("requestAction")]
        public string RequestAction { get; set; }

        [JsonProperty("requestBaseUrl")]
        public string RequestBaseUrl { get; set; }

        [JsonProperty("requestBeginPeriod")]
        public double RequestBeginPeriod { get; set; }

        [JsonProperty("requestBody")]
        public string RequestBody { get; set; }

        [JsonProperty("requestClient")]
        public string RequestClient { get; set; }

        [JsonProperty("requestClientIp")]
        public string RequestClientIp { get; set; }

        [JsonProperty("requestContentType")]
        public string RequestContentType { get; set; }

        [JsonProperty("requestController")]
        public string RequestController { get; set; }

        [JsonProperty("requestImei")]
        public string RequestImei { get; set; }

        [JsonProperty("requestMethod")]
        public string RequestMethod { get; set; }

        [JsonProperty("requestOpenId")]
        public string RequestOpenId { get; set; }

        [JsonProperty("requestQueryString")]
        public string RequestQueryString { get; set; }

        [JsonProperty("requestRawUrl")]
        public string RequestRawUrl { get; set; }

        [JsonProperty("requestReferer")]
        public string RequestReferer { get; set; }

        [JsonProperty("requestServerIp")]
        public string RequestServerIp { get; set; }

        [JsonProperty("requestServerName")]
        public string RequestServerName { get; set; }

        [JsonProperty("requestTime")]
        public DateTime RequestTime { get; set; }

        [JsonProperty("requestToken")]
        public string RequestToken { get; set; }

        [JsonProperty("requestTraceId")]
        public string RequestTraceId { get; set; }

        [JsonProperty("requestUrl")]
        public string RequestUrl { get; set; }

        [JsonProperty("requestUserAgent")]
        public string RequestUserAgent { get; set; }

        [JsonProperty("requestUserId")]
        public string RequestUserId { get; set; }

        [JsonProperty("requestVersion")]
        public string RequestVersion { get; set; }

        [JsonProperty("responseBody")]
        public string ResponseBody { get; set; }

        [JsonProperty("responseStatus")]
        public int ResponseStatus { get; set; }

        [JsonProperty("responseTime")]
        public DateTime ResponseTime { get; set; }

        //[JsonProperty("returnCode")]
        //public string ReturnCode { get; set; }
    }
}
