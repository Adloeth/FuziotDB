using System;
using System.Numerics;

namespace FuziotDB
{
    /*
     * 
     * Might not be used because it is a lot of work.
     * The idea is to have a variable integer that can be any size with a maximum of 2^16 bytes.
     * For example, you could save an RGB color as a 3 byte long integer. Or a 4x4 single floating point matrix with 64 bytes.
     *
     */

    [Obsolete]
    public struct VarUInt : IComparable<byte>, IComparable<ushort>, IComparable<uint>, IComparable<ulong>
    {
        /// <summary>
        /// Little Endian bytes
        /// </summary>
        private byte[] data;

        public int Size => data.Length;
        public byte this[int i] { get => data[i]; set => data[i] = value; }

        public VarUInt(byte[] data)
        {
            this.data = data;
        }

        public VarUInt(  byte data) : this(new byte[1] { data }) { }
        public VarUInt(ushort data) : this(BitConverter.GetBytes(data).ToLittleEndian()) { }
        public VarUInt(  uint data) : this(BitConverter.GetBytes(data).ToLittleEndian()) { }
        public VarUInt( ulong data) : this(BitConverter.GetBytes(data).ToLittleEndian()) { }

        private sbyte ToUByte() => (sbyte)data[0];
        private short ToUShort()
        {
            byte[] bytes = data.ToCurrentEndian(true);
            return BitConverter.ToInt16(bytes);
        }
        private int ToUInt()
        {
            byte[] bytes = data.ToCurrentEndian(true);
            return BitConverter.ToInt32(bytes);
        }
        private long ToULong()
        {
            byte[] bytes = data.ToCurrentEndian(true);
            return BitConverter.ToInt64(bytes);
        }

        private int GetLastNonZeroByte()
        {
            int last = 0;
            for (int i = 0; i < Size; i++)
            {
                byte b = data[i];

                if(b != 0x00)
                    last = i;
            }
            return last;
        }

        public int CompareTo(byte val)
        {
            if(val > 0)
            {
                int last = GetLastNonZeroByte();

                     if(last == 0) return val.CompareTo(0);
                else if(last == 1) return ToUByte  ().CompareTo(val);
                else if(last == 2) return ToUShort ().CompareTo(val);
                else if(last <= 4) return ToUInt   ().CompareTo(val);
                else if(last <= 8) return ToULong  ().CompareTo(val);
                else               return 1;
            }
            else
                return GetLastNonZeroByte() == 0 ? 0 : 1;
        }

        public int CompareTo(ushort val)
        {
            if(val > 0)
            {
                int last = GetLastNonZeroByte();

                     if(last == 0) return val.CompareTo(0);
                else if(last == 1) return ToUByte  ().CompareTo(val);
                else if(last == 2) return ToUShort ().CompareTo(val);
                else if(last <= 4) return ToUInt   ().CompareTo(val);
                else if(last <= 8) return ToULong  ().CompareTo(val);
                else               return 1;
            }
            else
                return GetLastNonZeroByte() == 0 ? 0 : 1;
        }

        public int CompareTo(uint val)
        {
            if(val > 0)
            {
                int last = GetLastNonZeroByte();

                     if(last == 0) return val.CompareTo(0);
                else if(last == 1) return ToUByte  ().CompareTo(val);
                else if(last == 2) return ToUShort ().CompareTo(val);
                else if(last <= 4) return ToUInt   ().CompareTo(val);
                else if(last <= 8) return ToULong  ().CompareTo(val);
                else               return 1;
            }
            else
                return GetLastNonZeroByte() == 0 ? 0 : 1;
        }

        public int CompareTo(ulong val)
        {
            if(val > 0)
            {
                int last = GetLastNonZeroByte();

                     if(last == 0) return val.CompareTo(0);
                else if(last == 1) return ToUByte  ().CompareTo(val);
                else if(last == 2) return ToUShort ().CompareTo(val);
                else if(last <= 4) return ToUInt   ().CompareTo(val);
                else if(last <= 8) return ToULong  ().CompareTo(val);
                else               return 1;
            }
            else
                return GetLastNonZeroByte() == 0 ? 0 : 1;
        }

        public override bool Equals(object obj)
        {            
            if (obj == null || !(obj is VarUInt var))
                return false;
            
            for (int i = 0; i < Math.Min(Size, var.Size); i++)
                if(var[i] != this[i])
                    return false;

            if(var.Size == Size)
                return true;

            if(Size < var.Size)
            {
                for (int i = Size; i < var.Size; i++)
                    if(var[i] != 0x00)
                        return false;
            }
            else
            {
                for (int i = var.Size; i < Size; i++)
                    if(this[i] != 0x00)
                        return false;
            }

            return true;
        }
        
        public override int GetHashCode() => data.GetHashCode();

        public static explicit operator byte[](VarUInt blob) => blob.data;
    }
    
    [Obsolete]
    public struct VarInt : IComparable<int>
    {
        /// <summary>
        /// Little Endian bytes
        /// </summary>
        private byte[] data;

        public int Size => data.Length;
        public bool IsNegative => (data[Size - 1] & 0b1000_0000) == 0b1000_0000;
        public bool IsPositive => (data[Size - 1] & 0b1000_0000) == 0;

        public byte this[int i] { get => data[i]; set => data[i] = value; }

        public VarInt(byte[] data)
        {
            this.data = data;
        }

        public VarInt(sbyte data) : this(new byte[1] { (byte)data }) { }
        public VarInt(short data) : this(BitConverter.GetBytes(data).ToLittleEndian()) { }
        public VarInt(  int data) : this(BitConverter.GetBytes(data).ToLittleEndian()) { }
        public VarInt( long data) : this(BitConverter.GetBytes(data).ToLittleEndian()) { }

        private sbyte ToByte() => (sbyte)data[0];
        private short ToShort()
        {
            byte[] bytes = data.ToCurrentEndian(true);
            return BitConverter.ToInt16(bytes);
        }
        private int ToInt()
        {
            byte[] bytes = data.ToCurrentEndian(true);
            return BitConverter.ToInt32(bytes);
        }
        private long ToLong()
        {
            byte[] bytes = data.ToCurrentEndian(true);
            return BitConverter.ToInt64(bytes);
        }
        private BigInteger ToBigInt() => new BigInteger(data);

        private int GetLastNonZeroByte()
        {
            int last = 0;
            for (int i = 0; i < Size; i++)
            {
                byte b = data[i];

                if(i == Size - 1)
                    b &= 0b0111_1111;

                if(b != 0x00)
                    last = i;
            }
            return last;
        }

        public int CompareTo(int val)
        {
            if(val > 0)
            {
                if(IsNegative)
                    return -1;

                int last = GetLastNonZeroByte();

                     if(last == 0 ) return val.CompareTo(0);
                else if(last == 1 ) return ToByte  ().CompareTo(val);
                else if(last == 2 ) return ToShort ().CompareTo(val);
                else if(last <= 4 ) return ToInt   ().CompareTo(val);
                else if(last <= 8 ) return ToLong  ().CompareTo(val);
                else if(last <= 16) return ToBigInt().CompareTo(val);
                else                return 1;
            }
            else if(val < 0)
            {
                if(IsPositive)
                    return 1;
                
                int last = GetLastNonZeroByte();

                     if(last == 0 ) return val.CompareTo(0);
                else if(last == 1 ) return ToByte  ().CompareTo(val);
                else if(last == 2 ) return ToShort ().CompareTo(val);
                else if(last <= 4 ) return ToInt   ().CompareTo(val);
                else if(last <= 8 ) return ToLong  ().CompareTo(val);
                else if(last <= 16) return ToBigInt().CompareTo(val);
                else                return -1;
            }
            else
                return GetLastNonZeroByte() == 0 ? 0 : IsPositive ? 1 : -1;
        }

        public override bool Equals(object obj)
        {            
            if (obj == null || !(obj is VarInt var))
                return false;
            
            if(var.Size != Size)
                return false;

            for (int i = 0; i < Size; i++)
                if(var[i] != this[i])
                    return false;

            return true;
        }
        
        public override int GetHashCode() => data.GetHashCode();

        public static explicit operator byte[](VarInt blob) => blob.data;
    }
}