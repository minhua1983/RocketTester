using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest.Framework;
using Newtonsoft.Json;

namespace RocketTester.ONS.Model
{
    [ESData]
    public class ConsumeData:BaseData
    {
        public ConsumeData()
            : base(IndexTypeEnum.CONSUMER_DATA)
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

        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("failureReason")]
        public string FailureReason { get; set; }

        [JsonProperty("consumedTimes")]
        public int ConsumedTimes { get; set; }

        [JsonProperty("shardingKey")]
        public string ShardingKey { get; set; }
    }
}
