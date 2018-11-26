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
    public class ProducerData : BaseData
    {
        public ProducerData(string requestTraceId)
            : base(IndexTypeEnum.PRODUCER_DATA, requestTraceId)
        {

        }

        [JsonProperty("accomplishment")]
        public bool Accomplishment { get; set; }

        [JsonProperty("applicationAlias")]
        public string ApplicationAlias { get; set; }

        [JsonProperty("topic")]
        public string Topic { get; set; }

        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("producerId")]
        public string ProducerId { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("transactionType")]
        public string TransactionType { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("serviceResult")]
        public bool ServiceResult { get; set; }

        [JsonProperty("transactionStatus")]
        public string TransactionStatus { get; set; }

        [JsonProperty("failureReason")]
        public string FailureReason { get; set; }

        [JsonProperty("producedTimes")]
        public int ProducedTimes { get; set; }

        [JsonProperty("shardingKey")]
        public string ShardingKey { get; set; }
    }
}
