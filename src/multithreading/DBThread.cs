using System;
using System.Threading;
using System.Collections.Generic;

namespace FuziotDB
{
    internal class DBThread : IDisposable
    {
        private Thread thread;
        private bool available;
        private bool closing;
        private ManualResetEvent mutex;

        public bool IsAvailable { get => available; set => available = value; }
        public bool Closing => closing;

        public DBThread(ManualResetEvent mutex, ThreadStart start)
        {
            this.mutex = mutex;
            thread = new Thread(start);
        }

        public void Start()
        {
            thread.Start();
        }

        public void Dispose()
        {
            closing = true;
            mutex.Set();
            thread.Join();
        }
    }
}