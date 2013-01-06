using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace ProducerClient
{
    class Temp
    {
        public ProducerClient Producer { set; get; }
        public int ThreadNO { set; get; }
    }
    class Program
    {
        static string hosts = "127.0.0.1,127.0.0.1:8001,127.0.0.1:8002";
        static ProducerClient producer = new ProducerClient(hosts);

        public static void SendProc(object temp)
        {
            Temp tmp = new Temp();
            tmp.Producer = ((Temp)temp).Producer;
            tmp.ThreadNO = ((Temp)temp).ThreadNO;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            for (int idx = 0; idx < 2000; idx++)
            {
                tmp.Producer.SendMessage("Hello world: {"+ tmp.ThreadNO + "}-[" + (idx+1) + "]");
                //Thread.Sleep(200);
            }
            stopWatch.Stop();
            Console.WriteLine("Thread {0}: {1}.", tmp.ThreadNO, stopWatch.Elapsed);
            Thread.EndThreadAffinity();
        }
        
        static void Main(string[] args)
        {
            producer.TopicName = "abc-abc";
            producer.IsACK = true;
            int numThreadNum = 5;

            for (int idx = 0; idx < numThreadNum; idx++)
            {
                Temp tmp = new Temp();
                tmp.Producer = producer;
                tmp.ThreadNO = idx + 1;
                Thread thread = new Thread(new ParameterizedThreadStart(Program.SendProc));
                thread.Start(tmp);
            }
            Thread.Sleep(5000);
            Environment.Exit(0);
        }
    }
}
