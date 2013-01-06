using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using ProducerClient.Internal;
using System.Threading;
using System.Diagnostics;

namespace ProducerClient
{
    class ProducerClient : Producer
    {
        private const int DEF_PORT = 8000;                  //默认Server端口号，如果Host中未指定Port，则使用该默认值
        private const int DEF_RECV_BUF_SIZE = 256;          //Receive Buffer大小
        private const int DEF_RECV_HEAD_LEN = 4;            //Receive Buffer中，前DEF_RECV_HEAD_LEN位标明Content长度
        private const int DEF_CONNECT_DELAY = 1000;

        private const int STATUS_OK = 250;                  //消息发送成功
        private const int STATUS_TOPICNAME_INVALID = 251;   //TopicName非法
        private const int STATUS_SAVE_FAILED = 252;         //保存至数据库出错

        private Random randSeed = new Random(Guid.NewGuid().GetHashCode());
        private DelayStrategy delay;
        private ProducerConfig config;
        private Mutex mut = new Mutex();                    //设置互斥变量，限制Punish和Update同时运行

        private IDictionary<EndPoint, int>        originalHostsAndWeights = new Dictionary<EndPoint, int>();
        private IDictionary<EndPoint, Socket>     availableHosts          = new Dictionary<EndPoint, Socket>();
        private ICollection<EndPoint>             punishedHosts           = new List<EndPoint>();

        private string topicName;
        public string TopicName 
        {
            set
            {
                if (!CheckTopicName(value)) { throw new ArgumentException("Topic name is invalid. Only \"^[a-zA-Z][0-9|a-z|A-Z|_|-]{1,29}$\" is supported"); }
                else { topicName = value; }
            }
            get { return topicName; }
        }
        public bool IsACK { set; get; }

        private bool CheckTopicName(string topicName)
        {
            Regex reg = new Regex("^[a-zA-Z][0-9|a-z|A-Z|_|-]{1,29}$");
            return reg.IsMatch(topicName);
        }

        public ProducerClient(string hosts) : this(null, hosts, null) { }

        public ProducerClient(string hosts, string weights) : this(null, hosts, weights) { }

        public ProducerClient(ProducerConfig config, string hosts) : this(config, hosts, null) { }
        /// <summary>
        /// ProducerClient构造函数
        /// </summary>
        /// <param name="config">Producer配置对象</param>
        /// <param name="hosts">ProducerServer地址URI，以","分隔</param>
        /// <param name="weights">ProducerServer权重，以","分隔，数量需与hosts一致，范围[0-10]</param>
        public ProducerClient(ProducerConfig config, string hosts, string weights)
        {
            if (config == null) this.config = new ProducerConfig();
            else this.config = config;
            Console.WriteLine(this.config);
            delay = new DelayStrategy(this.config.DelayBase, this.config.DelayMultiple);
            GenerateHostAndWeight(hosts, weights);
            ConnectToServers();
            new Thread(new ThreadStart(RecoveryProc)).Start();
        }
        /// <summary>
        /// 解析hosts和weights字符串
        /// </summary>
        /// <param name="hosts"></param>
        /// <param name="weights"></param>
        private void GenerateHostAndWeight(string hosts, string weights)
        {
            if (hosts == null) throw new ArgumentException("Hosts can not be null.");

            char[] trimChars = new char[] { ',', ' ', '\n' };
            string[] strHostArray = hosts.Trim(trimChars).Split(',');
            string[] strWeightArray = null;
            if (weights != null) { strWeightArray = weights.Trim(trimChars).Split(','); }
            else
            {
                strWeightArray = new string[strHostArray.Length];
                for (int idx = 0; idx < strWeightArray.Length; strWeightArray[idx] = "1", idx++) ;
            }
            
            if (strHostArray.Length != strWeightArray.Length)
            {
                string info = "Host= " + strHostArray.Length + " & Weight= " + strWeightArray.Length + ", mismatched.";
                throw new ArgumentException(info);
            }
            for (int idx = 0; idx < strHostArray.Length; idx++)
            {
                string[] strIpAndPortArray = strHostArray[idx].Trim().Split(':');
                int weight;
                try
                {
                    weight = int.Parse(strWeightArray[idx].Trim());
                }
                catch (System.Exception ex)
                {
                    throw new ArgumentException("Can not parse weight for " + (strIpAndPortArray.Length > 1 ? strIpAndPortArray[0] + ":" + strIpAndPortArray[1] : strIpAndPortArray[0]), ex);
                }
                if (weight > 10 || weight < 0)
                {
                    throw new ArgumentOutOfRangeException("Weight for " + strHostArray[idx] +" is " + weight + ", should be [0~10].");
                }
                if (strIpAndPortArray.Length == 1 || strIpAndPortArray[1].Equals(String.Empty))
                {
                    originalHostsAndWeights.Add(new IPEndPoint(IPAddress.Parse(strIpAndPortArray[0]), DEF_PORT), weight);
                    Console.WriteLine("Parse host: {0}:{1} = {2}.", strIpAndPortArray[0], DEF_PORT, weight);
                }
                else
                {
                    originalHostsAndWeights.Add(new IPEndPoint(IPAddress.Parse(strIpAndPortArray[0]), int.Parse(strIpAndPortArray[1])), weight);
                    Console.WriteLine("Parse host: {0}:{1} = {2}.", strIpAndPortArray[0], strIpAndPortArray[1], weight);
                }
            }
        }
        /// <summary>
        /// 与所有ProducerServer建立初始Socket连接
        /// </summary>
        private void ConnectToServers()
        {
            if(originalHostsAndWeights.Count() <= 0)
            {
                throw new ArgumentException("No host found.");
            }
            foreach (KeyValuePair<EndPoint, int> hostAndWeight in originalHostsAndWeights)
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SendTimeout = config.SendTimeout;
                socket.BeginConnect(hostAndWeight.Key, null, null);
                
                //异步连接Server，SendMessage时再检查Socket状态
                availableHosts.Add(hostAndWeight.Key, socket);
            }
        }
        /// <summary>
        /// 向ProducerServer发送消息
        /// </summary>
        /// <param name="content">待发送的消息</param>
        public void SendMessage(object content)
        {
            //重置延时
            delay.Reset();
            //生成SwallowMessage
            SwallowMessage swallowMessage = new SwallowMessage();
            swallowMessage.Content = content;
            swallowMessage.TopicName = TopicName;
            swallowMessage.IsACK = IsACK;

            //生成字节流
            byte[] msg = System.Text.Encoding.Default.GetBytes(JsonConvert.SerializeObject(swallowMessage));
            byte[] len = System.BitConverter.GetBytes(msg.Length);
            byte[] all = new byte[len.Length + msg.Length];

            Array.Reverse(len);
            Array.Copy(len, 0, all, 0, len.Length);
            Array.Copy(msg, 0, all, len.Length, msg.Length);

            bool sendSuccess = false;
            Socket hostAddr = null;
            byte[] recvBuf = new byte[DEF_RECV_BUF_SIZE];
            for (int tried = 0; tried < config.TryCount && !sendSuccess; tried++)
            {
                hostAddr = ChooseHost();
                if (hostAddr != null && hostAddr.Connected)
                {
                    try
                    {
                        mut.WaitOne();
                        hostAddr.Send(all);
                        if (IsACK)
                        {
                            hostAddr.Receive(recvBuf);
                            string ackJson = AckDecode(recvBuf);
                            if (ackJson != null)
                            {
                                SwallowACK ack = (SwallowACK)JsonConvert.DeserializeObject(ackJson, typeof(SwallowACK));
                                switch (ack.Status)
                                {
                                    case STATUS_OK:
                                        sendSuccess = true;
                                        break;
                                    case STATUS_TOPICNAME_INVALID:
                                        Console.WriteLine("Topic name is invalid.");
                                        break;
                                    case STATUS_SAVE_FAILED:
                                        Console.WriteLine("Can not save to Database now.");
                                        break;
                                    default:
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Can not parse buffer.");
                            }
                        }
                        else
                        {
                            sendSuccess = true;
                        }
                        mut.ReleaseMutex();
                    }
                    catch (Exception e)
                    {
                        mut.ReleaseMutex();
                        Console.WriteLine("Socket error: " + ((IPEndPoint)hostAddr.RemoteEndPoint).Address + ":" + ((IPEndPoint)hostAddr.RemoteEndPoint).Port);
                    }
                }
                if (!sendSuccess)//发送失败，惩罚host
                {
                    if (hostAddr != null)
                    {
                        mut.WaitOne();
                        IPEndPoint ip = (IPEndPoint)FindAddrBySocket(hostAddr);
                        if (!punishedHosts.Contains(ip))
                        {
                            punishedHosts.Add(ip);
                            availableHosts.Remove(ip);
                        }
                        mut.ReleaseMutex();
                    }
                    delay.Delay();
                }
            }
            if (!sendSuccess)
            {
                throw new SendFailedException("Can not send message in last " + config.TryCount + " times.");
            }
        }
        /// <summary>
        /// 从可用列表中选取ProducerServer以发送消息
        /// </summary>
        /// <returns>可用IP地址的Socket</returns>
        private Socket ChooseHost()
        {
            mut.WaitOne();
            int weightSum = GetWeightSum();
            if (weightSum <= 0)
            {
                return null;
            }
            int randWeightSum = randSeed.Next(weightSum);
            int curWeight = 0;
            Socket ret = null;
            foreach (KeyValuePair<EndPoint, Socket> tmpHost in availableHosts)
            {
                curWeight += originalHostsAndWeights[tmpHost.Key];
                if (randWeightSum < curWeight)
                {
                    ret = availableHosts[tmpHost.Key];
                    break;
                }
            }
            mut.ReleaseMutex();
            return ret;
        }
        /// <summary>
        /// 获取可用Hosts的权重之和
        /// </summary>
        /// <returns></returns>
        private int GetWeightSum()
        {
            if (availableHosts.Count == 0) { return -1; }
            int sum = 0;
            foreach (KeyValuePair<EndPoint, int> hostAndWeight in originalHostsAndWeights)
            {
                if (availableHosts.ContainsKey(hostAndWeight.Key))
                {
                    sum += hostAndWeight.Value;
                }
            }
            return sum;
        }
        /// <summary>
        /// 根据Socket中的LocalEndpoint字段，从可用Hosts中找到相应的RemoteEndpoint
        /// </summary>
        /// <param name="skt"></param>
        /// <returns></returns>
        private EndPoint FindAddrBySocket(Socket skt)
        {
            IPEndPoint localEndpoint = (IPEndPoint)skt.LocalEndPoint;
            foreach (KeyValuePair<EndPoint, Socket> hostAndSocket in availableHosts)
            {
                if (((IPEndPoint)hostAndSocket.Value.LocalEndPoint).Port == localEndpoint.Port) { return hostAndSocket.Key; }
            }
            return null;
        }
        /// <summary>
        /// 从byte[]中解析出获得的ACK的Json字符串
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private string AckDecode(byte[] buffer)
        {
            if (buffer.Length < DEF_RECV_HEAD_LEN) return null;
            string ret = null;
            try
            {
                Array.Reverse(buffer, 0, DEF_RECV_HEAD_LEN);
                ret = System.Text.Encoding.Default.GetString(buffer, DEF_RECV_HEAD_LEN, System.BitConverter.ToInt32(buffer, 0));
            }catch(Exception e){}
            return ret;
        }
        /// <summary>
        /// 回收线程执行的函数，用以从惩罚列表中回收可用的Hosts
        /// </summary>
        private void RecoveryProc()
        {
            IDictionary<EndPoint, IAsyncResult> mapIpResult = new Dictionary<EndPoint, IAsyncResult>();
            IDictionary<EndPoint, Socket> mapIpSocket = new Dictionary<EndPoint, Socket>();
            while (true)
            {
                mut.WaitOne();
                foreach (var punishedHost in punishedHosts)
                {
                    if(!mapIpResult.ContainsKey(punishedHost))
                    {
                        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        IAsyncResult result = socket.BeginConnect(punishedHost, null, null);
                        mapIpResult.Add(punishedHost, result);
                        mapIpSocket.Add(punishedHost, socket);
                    }
                }
                mut.ReleaseMutex();

                Thread.Sleep(DEF_CONNECT_DELAY);
                ICollection<EndPoint> listIps = new List<EndPoint>(mapIpResult.Keys);

                foreach (var ip in listIps)
                {
                    if (mapIpResult[ip].IsCompleted)
                    {
                        if (mapIpSocket[ip].Connected) 
                        {
                            mut.WaitOne();
                            availableHosts.Add(ip, mapIpSocket[ip]);
                            punishedHosts.Remove(ip);
                            Console.WriteLine("Reconnect to: " + ip + " successfully.");
                            mut.ReleaseMutex();
                        }
                        mapIpSocket.Remove(ip);
                        mapIpResult.Remove(ip);
                    }
                }
            }
        }
    }
}