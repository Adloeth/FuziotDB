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

    public class FetchAsyncInfo : ThreadInfo<List<DBVariant[]>, List<DBVariant[]>>
    {
        private List<DBVariant[]>[] results;
        private int finishedCount;

        public override bool IsFinished => finishedCount == results.Length;

        public FetchAsyncInfo(int threadCount)
        {
            finishedCount = 0;
            results = new List<DBVariant[]>[threadCount];
        }

        public override List<DBVariant[]> WaitForResult()
        {
            WaitUntilFinished();

            int size = 0;
            for (int i = 0; i < results.Length; i++)
                size = results[i].Count;

            List<DBVariant[]> result = new List<DBVariant[]>(size);

            for (int i = 0; i < results.Length; i++)
                result.AddRange(results[i]);

            return result;
        }

        internal override void SetResult(int i, List<DBVariant[]> result) 
        { 
            results[i] = result; 
            finishedCount++;
        }
    }
}