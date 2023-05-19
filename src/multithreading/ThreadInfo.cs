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

    public class FetchAsyncInfo : ThreadInfo<List<DBVariant[]>, DBVariant[][]>
    {
        private List<DBVariant[]>[] results;

        public override bool IsFinished 
        {
            get
            {
                for (int i = 0; i < results.Length; i++)
                    if(results[i] == null)
                        return false;

                return true;
            }
        }

        public FetchAsyncInfo(int threadCount)
        {
            results = new List<DBVariant[]>[threadCount];
        }

        public override DBVariant[][] WaitForResult()
        {
            WaitUntilFinished();

            List<DBVariant[]> result = new List<DBVariant[]>();

            for (int i = 0; i < results.Length; i++)
            {
                List<DBVariant[]> objects = results[i];
                result.AddRange(objects);
            }

            return result.ToArray();
        }

        internal override void SetResult(int i, List<DBVariant[]> result) => results[i] = result;
    }
}