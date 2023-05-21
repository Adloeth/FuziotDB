using System;
using System.Runtime.CompilerServices;

namespace FuziotDB
{
    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class DBSerializeAttribute : Attribute
    {
        private ASCIIS alias;
        private int length;
        private bool hasSize;
        private TranslatorBase converter;

        public ASCIIS Alias => alias;
        public int Length => length;
        public bool HasSize => hasSize;
        public TranslatorBase Converter => converter;

        public DBSerializeAttribute(string alias = "")
        {
            this.alias = new ASCIIS(alias);
            this.length = 0;
            hasSize = false;
        }

        public DBSerializeAttribute(int size)
        {
            this.alias = new ASCIIS("");
            this.length = size;
            hasSize = true;
        }

        public DBSerializeAttribute(string alias, int length)
        {
            this.alias = new ASCIIS(alias);
            this.length = length;
            hasSize = true;
        }

        public DBSerializeAttribute(string alias, TranslatorBase fixedConverter)
        {
            this.alias = new ASCIIS(alias);
            this.length = fixedConverter.Size;
            this.converter = fixedConverter;
            hasSize = true;
        }

        public DBSerializeAttribute(string alias, int length, TranslatorBase flexibleConverter)
        {
            this.alias = new ASCIIS(alias);
            this.length = length;
            this.converter = flexibleConverter;
            hasSize = true;
        }
    }
}