using BigInt = System.Numerics.BigInteger;

using System;
using System.Text;
using System.Numerics;
using System.Collections.Generic;

namespace FuziotDB
{
    public abstract class ConverterBase
    {
        private static readonly Dictionary<Type, ConverterBase> defaultConverters = new Dictionary<Type, ConverterBase> 
        {
            { typeof(  bool),    BoolConverter.Default },
            { typeof(  byte),   UInt8Converter.Default },
            { typeof( sbyte),    Int8Converter.Default },
            { typeof(ushort),  UInt16Converter.Default },
            { typeof( short),   Int16Converter.Default },
            { typeof(  uint),  UInt32Converter.Default },
            { typeof(   int),   Int32Converter.Default },
            { typeof( ulong),  UInt64Converter.Default },
            { typeof(  long),   Int64Converter.Default },
            { typeof(  Guid),    GuidConverter.Default },
            { typeof(BigInt),  BigIntConverter.Default },
            { typeof(  Half), Float16Converter.Default },
            { typeof( float), Float32Converter.Default },
            { typeof(double), Float64Converter.Default },
            { typeof(string),  StringConverter.Default },
            { typeof(ASCIIS),  ASCIISConverter.Default },
            { typeof(byte[]), ByteArrConverter.Default }
        };

        public static bool TryGetDefaultConverter(Type type, out ConverterBase converter) 
            => defaultConverters.TryGetValue(type, out converter);

        private Type type;
        private ushort size;
        private bool endianSensitive;
        private bool flexibleSize;

        internal ushort Size => size;
        internal bool EndianSensitive => endianSensitive;
        internal bool IsFlexible => flexibleSize;

        public ConverterBase(int byteCount, bool endianSensitive)
        {
            this.endianSensitive = endianSensitive;
            this.flexibleSize = byteCount <= 0;
            
            if(!flexibleSize)
            {
                if(byteCount > ushort.MaxValue)
                    throw new Exception(string.Concat("Invalid byte count, can't go higher than ", ushort.MaxValue));

                this.size = (ushort)(byteCount - 1);
            }
        }

        internal bool ValidType(Type type) => this.type == type;

        internal abstract byte[] ConvertFrom(object obj);
        internal abstract object ConvertTo(byte[] arr);

        internal abstract byte[] FlexibleConvertFrom(object obj, ushort size);
        internal abstract object FlexibleConvertTo(byte[] arr, ushort size);
    }

    public abstract class FlexibleConverter<T> : ConverterBase
    {
        internal override byte[] ConvertFrom(object obj) => throw new Exception("Invalid convert method : this converter has a flexible size, FlexibleConvertFrom() must be called instead.");
        internal override object ConvertTo(byte[] arr) => throw new Exception("Invalid convert method : this converter has a flexible size, FlexibleConvertTo() must be called instead.");

        internal override byte[] FlexibleConvertFrom(object obj, ushort size) => Serialize((T)obj, size + 1);
        internal override object FlexibleConvertTo(byte[] arr, ushort size) => (object)Deserialize(arr, size + 1);

        public FlexibleConverter(bool endianSensitive) : base(-1, endianSensitive) { }

        public abstract byte[] Serialize(T obj, int length);
        public abstract T Deserialize(byte[] data, int length);
    }

    public abstract class Converter<T> : ConverterBase
    {
        internal override byte[] ConvertFrom(object obj) => Serialize((T)obj);
        internal override object ConvertTo(byte[] arr) => (object)Deserialize(arr);

        internal override byte[] FlexibleConvertFrom(object obj, ushort size) => throw new Exception("Invalid convert method : this converter has a fixed size, ConvertFrom() must be called instead.");
        internal override object FlexibleConvertTo(byte[] arr, ushort size) => throw new Exception("Invalid convert method : this converter has a fixed size, ConvertTo() must be called instead.");

        public Converter(int byteCount, bool endianSensitive) : base(byteCount, endianSensitive) { }

        public abstract byte[] Serialize(T obj);
        public abstract T Deserialize(byte[] data);
    }

    #region DEFAULT CONVERTERS

    public sealed class BoolConverter : Converter<bool>
    {
        public static BoolConverter Default => new BoolConverter();

        public BoolConverter() : base(1, true) { }

        public override byte[] Serialize(bool obj) => new byte[1] { (byte)(obj ? 0xFF : 0x00) };
        public override bool Deserialize(byte[] data) => DBUtils.CountBits(data[0]) > 4 ? true : false;
    }

    public sealed class UInt8Converter : Converter<byte>
    {
        public static UInt8Converter Default => new UInt8Converter();

        public UInt8Converter() : base(1, true) { }

        public override byte[] Serialize(byte obj) => new byte[1] { obj };
        public override byte Deserialize(byte[] data) => data[0];
    }

    public sealed class Int8Converter : Converter<sbyte>
    {
        public static Int8Converter Default => new Int8Converter();

        public Int8Converter() : base(1, true) { }

        public override byte[] Serialize(sbyte obj) => new byte[1] { (byte)obj };
        public override sbyte Deserialize(byte[] data) => (sbyte)data[0];
    }

    public sealed class UInt16Converter : Converter<ushort>
    {
        public static UInt16Converter Default => new UInt16Converter();

        public UInt16Converter() : base(2, true) { }

        public override byte[] Serialize(ushort obj) => BitConverter.GetBytes(obj);
        public override ushort Deserialize(byte[] data) => BitConverter.ToUInt16(data);
    }

    public sealed class Int16Converter : Converter<short>
    {
        public static Int16Converter Default => new Int16Converter();

        public Int16Converter() : base(2, true) { }

        public override byte[] Serialize(short obj) => BitConverter.GetBytes(obj);
        public override short Deserialize(byte[] data) => BitConverter.ToInt16(data);
    }

    public sealed class UInt32Converter : Converter<uint>
    {
        public static UInt32Converter Default => new UInt32Converter();

        public UInt32Converter() : base(4, true) { }

        public override byte[] Serialize(uint obj) => BitConverter.GetBytes(obj);
        public override uint Deserialize(byte[] data) => BitConverter.ToUInt32(data);
    }

    public sealed class Int32Converter : Converter<int>
    {
        public static Int32Converter Default => new Int32Converter();

        public Int32Converter() : base(4, true) { }

        public override byte[] Serialize(int obj) => BitConverter.GetBytes(obj);
        public override int Deserialize(byte[] data) => BitConverter.ToInt32(data);
    }

    public sealed class UInt64Converter : Converter<ulong>
    {
        public static UInt64Converter Default => new UInt64Converter();

        public UInt64Converter() : base(8, true) { }

        public override byte[] Serialize(ulong obj) => BitConverter.GetBytes(obj);
        public override ulong Deserialize(byte[] data) => BitConverter.ToUInt64(data);
    }

    public sealed class Int64Converter : Converter<long>
    {
        public static Int64Converter Default => new Int64Converter();

        public Int64Converter() : base(8, true) { }

        public override byte[] Serialize(long obj) => BitConverter.GetBytes(obj);
        public override long Deserialize(byte[] data) => BitConverter.ToInt64(data);
    }

    public sealed class GuidConverter : Converter<Guid>
    {
        public static GuidConverter Default => new GuidConverter();

        public GuidConverter() : base(16, true) { }

        public override byte[] Serialize(Guid obj) => obj.ToByteArray();
        public override Guid Deserialize(byte[] data) => new Guid(data);
    }

    public sealed class BigIntConverter : Converter<BigInteger>
    {
        public static BigIntConverter Default => new BigIntConverter();

        public BigIntConverter() : base(16, false) { }

        public override byte[] Serialize(BigInteger obj) => obj.ToByteArray();
        public override BigInteger Deserialize(byte[] data) => new BigInteger(data);
    }

    public sealed class Float16Converter : Converter<Half>
    {
        public static Float16Converter Default => new Float16Converter();

        public Float16Converter() : base(2, true) { }

        public override byte[] Serialize(Half obj) => BitConverter.GetBytes(obj);
        public override Half Deserialize(byte[] data) => BitConverter.ToHalf(data);
    }

    public sealed class Float32Converter : Converter<float>
    {
        public static Float32Converter Default => new Float32Converter();

        public Float32Converter() : base(4, true) { }

        public override byte[] Serialize(float obj) => BitConverter.GetBytes(obj);
        public override float Deserialize(byte[] data) => BitConverter.ToSingle(data);
    }

    public sealed class Float64Converter : Converter<double>
    {
        public static Float64Converter Default => new Float64Converter();

        public Float64Converter() : base(8, true) { }

        public override byte[] Serialize(double obj) => BitConverter.GetBytes(obj);
        public override double Deserialize(byte[] data) => BitConverter.ToDouble(data);
    }

    public sealed class StringConverter : FlexibleConverter<string>
    {
        public static StringConverter Default => new StringConverter();

        public StringConverter() : base(false) { }

        public override byte[] Serialize(string obj, int length) => Encoding.Unicode.GetBytes(obj, 0, length);
        public override string Deserialize(byte[] data, int length) => Encoding.Unicode.GetString(data, 0, length);
    }

    public sealed class ASCIISConverter : FlexibleConverter<ASCIIS>
    {
        public static ASCIISConverter Default => new ASCIISConverter();

        public ASCIISConverter() : base(false) { }

        public override byte[] Serialize(ASCIIS obj, int length) => Encoding.ASCII.GetBytes(obj, 0, length);
        public override ASCIIS Deserialize(byte[] data, int length) => new ASCIIS(Encoding.ASCII.GetString(data, 0, length));
    }

    public sealed class ByteArrConverter : FlexibleConverter<byte[]>
    {
        public static ByteArrConverter Default => new ByteArrConverter();

        public ByteArrConverter() : base(false) { }

        public override byte[] Serialize(byte[] obj, int length) => obj.EnsureSize(length);
        public override byte[] Deserialize(byte[] data, int length) => data.EnsureSize(length);
    }

    #endregion
}