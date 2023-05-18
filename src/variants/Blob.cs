using System;

namespace FuziotDB
{
    /// <summary>
    /// A binary large object that is not subject to endianness when serialized or deserialized. Mostly used to handle files in the database.
    /// </summary>
    public struct Blob : IEquatable<byte[]>
    {
        private byte[] data;

        public int Length => data.Length;

        public byte this[int i] { get => data[i]; set => data[i] = value; }

        public Blob(byte[] data, int length)
        {
            byte[] bytes = new byte[length];
            Array.Copy(data, bytes, Math.Min(data.Length, length));
            
            this.data = bytes;
        }

        public bool Equals(byte[] data) => this.data == data;

        public override bool Equals(object obj)
        {            
            if (obj == null || !(obj is byte[] data))
                return false;
            
            return this.data == data;
        }
        
        public override int GetHashCode() => data.GetHashCode();

        public static implicit operator byte[](Blob blob) => blob.data;
    }
}