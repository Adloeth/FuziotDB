using System.Threading;
using System.Collections.Generic;

namespace FuziotDB
{
    public abstract class ThreadInfoBase
    {
        protected ManualResetEventSlim resetEvent;
        public void WaitUntilFinished() { resetEvent.Wait(); }
        public abstract bool IsFinished { get; }

        public ThreadInfoBase()
        {
            resetEvent = new ManualResetEventSlim(false);
        }
    }

    public abstract class ThreadInfo<ThreadResult, FinalResult> : ThreadInfoBase
    {
        protected ThreadResult[] results;
        protected int finishedCount;

        public ThreadInfo(int threadCount) : base() 
        { 
            results = new ThreadResult[threadCount];
            finishedCount = 0;
        }

        public abstract FinalResult WaitForResult();

        internal void SetResult(int i, ThreadResult result)
        {
            lock(results)
            {
                results[i] = result;
                finishedCount++;

                if(IsFinished)
                    resetEvent.Set();
            }
        }
    }

    public class FetchAsyncInfo : ThreadInfo<List<object[]>, List<object[]>>
    {
        public override bool IsFinished => finishedCount == results.Length;

        public FetchAsyncInfo(int threadCount) : base(threadCount) { }

        public override List<object[]> WaitForResult()
        {
            WaitUntilFinished();

            int size = 0;
            for (int i = 0; i < results.Length; i++)
                size = results[i].Count;

            List<object[]> result = new List<object[]>(size);

            for (int i = 0; i < results.Length; i++)
                result.AddRange(results[i]);

            return result;
        }
    }

    public class FetchFullAsyncInfo<T> : ThreadInfo<List<T>, List<T>>
    {
        public override bool IsFinished => finishedCount == results.Length;

        public FetchFullAsyncInfo(int threadCount) : base(threadCount) { }

        public override List<T> WaitForResult()
        {
            WaitUntilFinished();

            int size = 0;
            for (int i = 0; i < results.Length; i++)
                size = results[i].Count;

            List<T> result = new List<T>(size);

            for (int i = 0; i < results.Length; i++)
                result.AddRange(results[i]);

            return result;
        }
    }

    public class CountAsyncInfo : ThreadInfo<long, long>
    {
        public override bool IsFinished => finishedCount == results.Length;

        public CountAsyncInfo(int threadCount) : base(threadCount) { }

        public override long WaitForResult()
        {
            WaitUntilFinished();

            long result = 0;

            for (int i = 0; i < results.Length; i++)
                result += results[i];

            return result;
        }
    }
}