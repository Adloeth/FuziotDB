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
    }
}