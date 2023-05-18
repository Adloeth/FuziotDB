using System;
using System.Threading;
using System.Collections.Generic;

namespace FuziotDB
{
    internal class DBThread : IDisposable
    {
        private Thread thread;
        private DBThreadResult result;
        private bool available;
        private bool closing;
        private ManualResetEvent mutex;

        public DBThreadResult Result { get => result; set => result = value; }
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
    
    public class DBThreadResult
    {
        public virtual void MergeTo(DBThreadResult result) { }
    }

    public class FetchResult : DBThreadResult
    {
        public List<DBVariant[]> result;

        public FetchResult()
        {
            result = new List<DBVariant[]>();
        }

        public FetchResult(List<DBVariant[]> result)
        {
            this.result = result;
        }

        public override void MergeTo(DBThreadResult result)
        {
            this.result.AddRange(((FetchResult)result).result);
        }
    }
}