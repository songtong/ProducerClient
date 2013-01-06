using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ProducerClient
{
    class SwallowMessage
    {
        private string topicName;
        private bool isAck;
        private Type contentType;

        [JsonProperty(PropertyName = "content")]
        private string content;
        [JsonProperty(PropertyName = "topic")]
        public string TopicName
        {
            set { topicName = value; }
            get { return topicName; }
        }
        public object Content
        {
            set { content = value.GetType() == typeof(string) ? (string)value : JsonConvert.SerializeObject(value); contentType = value.GetType(); }
            get { if (contentType == typeof(string)) return content; else return JsonConvert.DeserializeObject(content, contentType); }
        }
        [JsonProperty(PropertyName = "isACK")]
        public bool IsACK
        {
            set { isAck = value; }
            get { return isAck; }
        }
        public string GetContent()
        {
            return content;
        }
    }
}
