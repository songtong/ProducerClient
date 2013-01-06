using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProducerClient
{
    class ProducerConfig
    {
        public const int DEFAULT_TRY_COUNT = 5;                //发送尝试次数
        public const int DEFAULT_SEND_TIMEOUT = 2000;          //发送等待延时
        public const int DEFAULT_CONNECT_TIMEOUT = 1;          //连接等待延时
        public const int DEFAULT_DELAY_BASE = 500;             //发送重试延时基数
        public const int DEFAULT_DELAY_MULTIPLE = 5;           //发送重试延时倍数

        private int tryCount = DEFAULT_TRY_COUNT;
        private int sendTimeout = DEFAULT_SEND_TIMEOUT;
        private int connectTimeout = DEFAULT_CONNECT_TIMEOUT;
        private int delayBase = DEFAULT_DELAY_BASE;
        private int delayMultiple = DEFAULT_DELAY_MULTIPLE;

        public int TryCount { set { tryCount = value > 0 ? value : DEFAULT_TRY_COUNT; } get { return tryCount; } }
        public int SendTimeout { set { sendTimeout = value >= 0 ? value : DEFAULT_SEND_TIMEOUT; } get { return sendTimeout; } }
        public int ConnectTimeout { set { connectTimeout = value >= 0 ? value : DEFAULT_CONNECT_TIMEOUT; } get { return connectTimeout; } }
        public int DelayBase { set { delayBase = value >= 0 && delayBase * delayMultiple < int.MaxValue ? value : DEFAULT_DELAY_BASE; } get { return delayBase; } }
        public int DelayMultiple { set { delayMultiple = value >= 0 && delayBase * delayMultiple < int.MaxValue ? value : DEFAULT_DELAY_MULTIPLE; } get { return delayMultiple; } }

        public override string ToString()
        {
            return "ProducerConfig: [ TryCount= " + TryCount + "; SendTimeout= " + SendTimeout +"; ConnectTimeout= " + connectTimeout + "; DelayBase= " + DelayBase + "; DelayMultiple= " + DelayMultiple + " ]";
        }
    }
}