using System;
using System.Collections.Generic;

namespace FuziotDB
{
    public abstract class ThreadInfoBase
    {
        public abstract bool IsFinished { get; }
    }

    public abstract class ThreadInfo<ThreadResult, FinalResult> : ThreadInfoBase
    {
        public abstract FinalResult WaitForResult();

        protected void WaitUntilFinished() { while(!IsFinished); }

        internal abstract void SetResult(int i, ThreadResult result);
    }

    public class FetchAsyncInfo : ThreadInfo<List<object[]>, List<object[]>>
    {
        private List<object[]>[] results;
        private int finishedCount;

        public override bool IsFinished => finishedCount == results.Length;

        public FetchAsyncInfo(int threadCount)
        {
            finishedCount = 0;
            results = new List<object[]>[threadCount];
        }

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

        internal override void SetResult(int i, List<object[]> result) 
        { 
            lock(results)
            {
                results[i] = result;
                finishedCount++;
            }
        }
    }

    public class FetchFullAsyncInfo<T> : ThreadInfo<List<T>, List<T>>
    {
        private List<T>[] results;
        private int finishedCount;

        public override bool IsFinished => finishedCount == results.Length;

        public FetchFullAsyncInfo(int threadCount)
        {
            finishedCount = 0;
            results = new List<T>[threadCount];
        }

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

        internal override void SetResult(int i, List<T> result) 
        { 
            results[i] = result; 
            finishedCount++;
        }
    }

    public class CountAsyncInfo : ThreadInfo<long, long>
    {
        private long[] results;
        private int finishedCount;

        public override bool IsFinished => finishedCount == results.Length;

        public CountAsyncInfo(int threadCount)
        {
            finishedCount = 0;
            results = new long[threadCount];
        }

        public override long WaitForResult()
        {
            WaitUntilFinished();

            long result = 0;

            for (int i = 0; i < results.Length; i++)
                result += results[i];

            return result;
        }

        internal override void SetResult(int i, long result) 
        { 
            results[i] = result; 
            finishedCount++;
        }
    }
}