using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ProducerClient
{
    class SwallowACK
    {
        [JsonProperty(PropertyName = "status")]
        private int status;

        [JsonProperty(PropertyName = "info")]
        private string info;
        
        public int Status
        {
            set { status = value; }
            get { return status; }
        }

        public string Information
        {
            set { info = value; }
            get { return info; }
        }
    }
}
