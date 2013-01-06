using System;
using System.Threading;

namespace ProducerClient.Internal
{
    class DelayStrategy
    {
        private int delayBase;
        private int multiple;
        private int delayTime;
        public DelayStrategy(int delayBase, int multiple)
        {
            this.delayBase = delayBase >= 0 ? delayBase : 0;
            this.multiple = multiple >= 0 ? multiple : 0;
            this.delayTime = this.delayBase;
        }
        public void Delay()
        {
            Console.WriteLine("I will sleep for: " + delayTime + ".");
            Thread.Sleep(delayTime);
            delayTime = delayTime >= delayBase * multiple ? delayBase * multiple : delayTime + delayBase;
        }
        public void Reset() { delayTime = delayBase; }
    }
}