using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest.Framework;
using Newtonsoft.Json;

namespace RocketTester.ONS
{
    [ESData]
    public class ConsumerData : BaseData
    {
        public ConsumerData(string requestTraceId)
            : base(IndexTypeEnum.CONSUMER_DATA, requestTraceId)
        {

        }

        [JsonProperty("accomplishment")]
        public int Accomplishment { get; set; }

        [JsonProperty("applicationAlias")]
        public string ApplicationAlias { get; set; }

        [JsonProperty("consumedStatus")]
        public string ConsumedStatus { get; set; }

        [JsonProperty("topic")]
        public string Topic { get; set; }

        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("producerId")]
        public string ProducerId { get; set; }

        [JsonProperty("consumerId")]
        public string ConsumerId { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("failureReason")]
        public string FailureReason { get; set; }

        [JsonProperty("consumedTimes")]
        public int ConsumedTimes { get; set; }

        [JsonProperty("shardingKey")]
        public string ShardingKey { get; set; }

        [JsonProperty("requestTraceId")]
        public string RequestTraceId { get; set; }
    }
}
