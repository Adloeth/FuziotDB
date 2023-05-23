using System;

namespace FuziotDB
{
    internal abstract class DatabaseAction
    {
        public Type type;

        public DatabaseAction(Type type)
        {
            this.type = type;
        }

        public abstract ThreadInfoBase GenerateInfo(int threadCount);
        public abstract void Execute(Database database, int threadCount, int threadID, ThreadInfoBase baseInfo);
    }

    internal class FetchAction : DatabaseAction
    {
        public Database.FetchFunc fetchFunc;
        public string[] fieldsToSearch;

        public FetchAction(Type type, Database.FetchFunc fetchFunc, string[] fieldsToSearch) : base(type)
        {
            this.fetchFunc = fetchFunc;
            this.fieldsToSearch = fieldsToSearch;
        }

        public override ThreadInfoBase GenerateInfo(int threadCount) => new FetchAsyncInfo(threadCount);

        public override void Execute(Database database, int threadCount, int threadID, ThreadInfoBase baseInfo)
            => ((FetchAsyncInfo)baseInfo).SetResult(threadID, database.InternalAsyncFetch(type, fetchFunc, fieldsToSearch, threadCount, threadID));
    }

    internal class CancellableFetchAction : DatabaseAction
    {
        public Database.CancellableFetchFunc fetchFunc;
        public string[] fieldsToSearch;

        public CancellableFetchAction(Type type, Database.CancellableFetchFunc fetchFunc, string[] fieldsToSearch) : base(type)
        {
            this.fetchFunc = fetchFunc;
            this.fieldsToSearch = fieldsToSearch;
        }

        public override ThreadInfoBase GenerateInfo(int threadCount) => new FetchAsyncInfo(threadCount);

        public override void Execute(Database database, int threadCount, int threadID, ThreadInfoBase baseInfo)
            => ((FetchAsyncInfo)baseInfo).SetResult(threadID, database.InternalAsyncFetch(type, fetchFunc, fieldsToSearch, threadCount, threadID));
    }

    internal class FetchFullAction<T> : DatabaseAction
    {
        public Database.FetchFunc<T> fetchFunc;

        public FetchFullAction(Database.FetchFunc<T> fetchFunc) : base(typeof(T))
        {
            this.fetchFunc = fetchFunc;
        }

        public override ThreadInfoBase GenerateInfo(int threadCount) => new FetchFullAsyncInfo<T>(threadCount);

        public override void Execute(Database database, int threadCount, int threadID, ThreadInfoBase baseInfo)
            => ((FetchFullAsyncInfo<T>)baseInfo).SetResult(threadID, database.InternalAsyncFetchFull(type, fetchFunc, threadCount, threadID));
    }

    internal class CancellableFetchFullAction<T> : DatabaseAction
    {
        public Database.CancellableFetchFunc<T> fetchFunc;

        public CancellableFetchFullAction(Database.CancellableFetchFunc<T> fetchFunc) : base(typeof(T))
        {
            this.fetchFunc = fetchFunc;
        }

        public override ThreadInfoBase GenerateInfo(int threadCount) => new FetchFullAsyncInfo<T>(threadCount);

        public override void Execute(Database database, int threadCount, int threadID, ThreadInfoBase baseInfo)
            => ((FetchFullAsyncInfo<T>)baseInfo).SetResult(threadID, database.InternalAsyncFetchFull(type, fetchFunc, threadCount, threadID));
    }

    internal class FetchCountAction : FetchAction
    {
        public FetchCountAction(Type type, Database.FetchFunc fetchFunc, string[] fieldsToSearch) : base(type, fetchFunc, fieldsToSearch)
        { }

        public override ThreadInfoBase GenerateInfo(int threadCount) => new CountAsyncInfo(threadCount);

        public override void Execute(Database database, int threadCount, int threadID, ThreadInfoBase baseInfo)
            => ((CountAsyncInfo)baseInfo).SetResult(threadID, database.InternalAsyncCount(type, fetchFunc, fieldsToSearch, threadCount, threadID));
    }

    internal class CancellableFetchCountAction : CancellableFetchAction
    {
        public CancellableFetchCountAction(Type type, Database.CancellableFetchFunc fetchFunc, string[] fieldsToSearch) : base(type, fetchFunc, fieldsToSearch)
        { }

        public override ThreadInfoBase GenerateInfo(int threadCount) => new CountAsyncInfo(threadCount);

        public override void Execute(Database database, int threadCount, int threadID, ThreadInfoBase baseInfo)
            => ((CountAsyncInfo)baseInfo).SetResult(threadID, database.InternalAsyncCount(type, fetchFunc, fieldsToSearch, threadCount, threadID));
    }
}