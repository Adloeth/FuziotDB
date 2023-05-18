using System;
using System.Text;

namespace FuziotDB
{
    /// <summary>
    /// A string that is guaranteed to be ASCII.
    /// </summary>
    public struct ASCIIS : IEquatable<string>
    {
        private string data = "";

        public int Length => data.Length;

        public byte this[int i] => (byte)data[i];

        public ASCIIS(string str)
        {
            if(!str.IsASCIICompatible())
                throw new Exception("Cannot create ASCIIString from string because it contains non-ASCII chars.");

            data = str;
        }

        public ASCIIS(byte[] bytes)
        {
            data = Encoding.ASCII.GetString(bytes);
        }

        public bool Equals(string str) => data == str;

        public override bool Equals(object obj)
        {            
            if (obj == null || !(obj is string || obj is ASCIIS))
                return false;
            
            return data == (string)obj;
        }

        public ASCIIS Substring(int startIndex) => new ASCIIS(data.Substring(startIndex));
        public ASCIIS Substring(int startIndex, int length) => new ASCIIS(data.Substring(startIndex, length));

        public static bool IsNullOrEmpty(ASCIIS str) => string.IsNullOrEmpty(str.data);
        
        public override int GetHashCode() => data.GetHashCode();

        public static implicit operator string(ASCIIS str) => str.data;
        public static explicit operator ASCIIS(string str) => new ASCIIS(str);

        public override string ToString() => data;
    }
}