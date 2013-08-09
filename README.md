ProducerClient
==============

swallow producer client c# version

用法：

ProducerClient C#版本提供四个构造函数如下：

        public ProducerClient(string hosts);
        public ProducerClient(string hosts, string weights);
        public ProducerClient(ProducerConfig config, string hosts);
        public ProducerClient(ProducerConfig config, string hosts, string weights)
        
其中：
        参数config为ProducerConfig对象，用以配置ProducerClient连接ProducerServer的方式
        参数hosts为ProducerServer地址URI，以","分隔
        参数weights为ProducerServer权重，以","分隔，数量需与hosts一致，范围[0-10]

如果不提供ProducerConfig参数，则使用默认值，默认配置如下：

        public const int DEFAULT_TRY_COUNT = 5;                //发送失败时的重试次数
        public const int DEFAULT_SEND_TIMEOUT = 2000;          //发送至Server端的延时
        public const int DEFAULT_CONNECT_TIMEOUT = 1;          //连接至Server端的延时
        public const int DEFAULT_DELAY_BASE = 500;             //发送重试延时基数
        public const int DEFAULT_DELAY_MULTIPLE = 5;           //发送重试延时倍数
        
其中：
        发送重试有延时，范围从delay_base直到delay_multiple * delay_base，每次重试延时增加1倍，直至最大

用法示例：
        
        string hosts = "127.0.0.1,127.0.0.1:8001,127.0.0.1:8002";//设置需要连接的ProducerServer
        ProducerClient producer = new ProducerClient(hosts);//声明ProducerClient对象
        producer.TopicName = "abc-abc";//设置想要发送的Topic
        producer.IsACK = true;//设置是否需要ProducerServer端的ACK
        producer.SendMessage("Hello world");//发送消息

        
