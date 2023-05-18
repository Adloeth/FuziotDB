using System;
using System.Runtime.CompilerServices;

namespace FuziotDB
{
    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class DBSerializeAttribute : Attribute
    {
        private ASCIIS alias;
        private int size;
        private bool hasSize;

        public ASCIIS Alias => alias;
        public int Size => size;
        public bool HasSize => hasSize;

        public DBSerializeAttribute(string alias = "")
        {
            this.alias = new ASCIIS(alias);
            this.size = 0;
            hasSize = false;
        }

        public DBSerializeAttribute(int size)
        {
            this.alias = new ASCIIS("");
            this.size = size;
            hasSize = true;
        }

        public DBSerializeAttribute(string alias, int size)
        {
            this.alias = new ASCIIS(alias);
            this.size = size;
            hasSize = true;
        }
    }

    /*public interface IDBObject
    {
        public uint DBID { get; set; }

        public abstract void Serialize(DBSerializer fields);
    }

    public interface IDBObject<T> : IDBObject where T : IDBObject<T>
    {
        public abstract static void Register(DBRegistry<T> registry);
    }*/
}