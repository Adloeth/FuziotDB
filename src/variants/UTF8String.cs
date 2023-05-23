using System;

namespace FuziotDB
{
    public struct UTF8S
    {
        private string data = "";

        public int Length => data.Length;

        public byte this[int i] => (byte)data[i];

        public UTF8S(string str)
        {
            data = str;
        }

        public UTF8S(byte[] bytes)
        {
            data = System.Text.Encoding.UTF8.GetString(bytes);
        }

        public bool Equals(string str) => data == str;

        public override bool Equals(object obj)
        {            
            if (obj == null || !(obj is string || obj is UTF8S))
                return false;
            
            return data == (string)obj;
        }

        public UTF8S Substring(int startIndex) => new UTF8S(data.Substring(startIndex));
        public UTF8S Substring(int startIndex, int length) => new UTF8S(data.Substring(startIndex, length));

        public static bool IsNullOrEmpty(UTF8S str) => string.IsNullOrEmpty(str.data);
        
        public override int GetHashCode() => data.GetHashCode();

        public static implicit operator string(UTF8S str) => str.data;
        public static explicit operator UTF8S(string str) => new UTF8S(str);

        public override string ToString() => data;
    }
}