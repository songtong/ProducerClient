using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProducerClient
{
    class SendFailedException : Exception
    {
        public SendFailedException(string message) : base(message) { }
    }
}
