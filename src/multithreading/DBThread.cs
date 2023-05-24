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
        private ManualResetEventSlim resetEvent;

        public bool IsAvailable { get => available; set => available = value; }
        public bool Closing => closing;

        public DBThread(ManualResetEventSlim resetEvent, ThreadStart start)
        {
            this.resetEvent = resetEvent;
            thread = new Thread(start);
        }

        public void Start()
        {
            thread.Start();
        }

        public void Dispose()
        {
            closing = true;
            resetEvent.Set();
            thread.Join();
        }
    }
}