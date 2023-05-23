using BigInt = System.Numerics.BigInteger;

using System;
using System.Text;
using System.Numerics;
using System.Collections.Generic;

namespace FuziotDB
{
    public abstract class TranslatorBase
    {
        private static readonly Dictionary<Type, TranslatorBase> defaultTranslators = new Dictionary<Type, TranslatorBase> 
        {
            { typeof(  bool),    BoolTranslator.Default },
            { typeof(  byte),   UInt8Translator.Default },
            { typeof( sbyte),    Int8Translator.Default },
            { typeof(ushort),  UInt16Translator.Default },
            { typeof( short),   Int16Translator.Default },
            { typeof(  uint),  UInt32Translator.Default },
            { typeof(   int),   Int32Translator.Default },
            { typeof( ulong),  UInt64Translator.Default },
            { typeof(  long),   Int64Translator.Default },
            { typeof(  Guid),    GuidTranslator.Default },
            { typeof(BigInt),  BigIntTranslator.Default },
            { typeof(  Half), Float16Translator.Default },
            { typeof( float), Float32Translator.Default },
            { typeof(double), Float64Translator.Default },
            { typeof(string),  StringTranslator.Default },
            { typeof(ASCIIS),  ASCIISTranslator.Default },
            { typeof(byte[]), ByteArrTranslator.Default }
        };

        public static bool TryGetDefaultTranslator(Type type, out TranslatorBase translator) 
            => defaultTranslators.TryGetValue(type, out translator);

        private Type type;
        private ushort size;
        private bool endianSensitive;
        private bool flexibleSize;

        internal ushort Size => size;
        internal bool EndianSensitive => endianSensitive;
        internal bool IsFlexible => flexibleSize;

        public TranslatorBase(Type type, int byteCount, bool endianSensitive)
        {
            this.type = type;
            this.endianSensitive = endianSensitive;
            this.flexibleSize = byteCount <= 0;
            
            if(!flexibleSize)
            {
                if(byteCount > ushort.MaxValue)
                    throw new Exception(string.Concat("Invalid byte count, can't go higher than ", ushort.MaxValue));

                this.size = (ushort)(byteCount - 1);
            }
        }

        /// <summary>
        /// How many bytes each elements has. For example, when serializing an UTF-16 string, BytesPerElement would be equal to 2. While a byte array
        /// or an ASCII string, will have BytesPerElement equal to 1, an int array will have it at 4.
        /// <br></br>
        /// This is used because <see cref="Serialize"/> and <see cref="Deserialize"/> are using a length (how many elements) and not a size 
        /// (how many bytes - 1) to make it easier to use.
        /// </summary>
        /// <value></value>
        public abstract byte BytesPerElement { get; }

        internal bool ValidType(Type type) => this.type == type;

        internal abstract byte[] FixedTranslateFrom(object obj);
        internal abstract object FixedTranslateTo(byte[] arr);

        internal abstract byte[] FlexibleTranslateFrom(object obj, ushort size);
        internal abstract object FlexibleTranslateTo(byte[] arr, ushort size);
    }

    public abstract class FlexibleTranslator<T> : TranslatorBase
    {
        internal override byte[] FixedTranslateFrom(object obj) => throw new Exception("Invalid convert method : this translator has a flexible size, FlexibleConvertFrom() must be called instead.");
        internal override object FixedTranslateTo(byte[] arr) => throw new Exception("Invalid convert method : this translator has a flexible size, FlexibleConvertTo() must be called instead.");

        internal override byte[] FlexibleTranslateFrom(object obj, ushort size) => Serialize((T)obj, (size + 1) / BytesPerElement);
        internal override object FlexibleTranslateTo(byte[] arr, ushort size) => (object)Deserialize(arr, (size + 1) / BytesPerElement);

        public FlexibleTranslator(bool endianSensitive) : base(typeof(T), -1, endianSensitive) { }

        public abstract byte[] Serialize(T obj, int length);
        public abstract T Deserialize(byte[] data, int length);
    }

    public abstract class FixedTranslator<T> : TranslatorBase
    {
        internal override byte[] FixedTranslateFrom(object obj) => Serialize((T)obj);
        internal override object FixedTranslateTo(byte[] arr) => (object)Deserialize(arr);

        internal override byte[] FlexibleTranslateFrom(object obj, ushort size) => throw new Exception("Invalid convert method : this translator has a fixed size, ConvertFrom() must be called instead.");
        internal override object FlexibleTranslateTo(byte[] arr, ushort size) => throw new Exception("Invalid convert method : this translator has a fixed size, ConvertTo() must be called instead.");

        public override byte BytesPerElement => 0;

        public FixedTranslator(int byteCount, bool endianSensitive) : base(typeof(T), byteCount, endianSensitive) { }

        public abstract byte[] Serialize(T obj);
        public abstract T Deserialize(byte[] data);
    }

    #region DEFAULT TRANSLATORS

    public sealed class BoolTranslator : FixedTranslator<bool>
    {
        public static BoolTranslator Default => new BoolTranslator();

        public BoolTranslator() : base(1, true) { }

        public override byte[] Serialize(bool obj) => new byte[1] { (byte)(obj ? 0xFF : 0x00) };
        public override bool Deserialize(byte[] data) => DBUtils.CountBits(data[0]) > 4 ? true : false;
    }

    public sealed class UInt8Translator : FixedTranslator<byte>
    {
        public static UInt8Translator Default => new UInt8Translator();

        public UInt8Translator() : base(1, true) { }

        public override byte[] Serialize(byte obj) => new byte[1] { obj };
        public override byte Deserialize(byte[] data) => data[0];
    }

    public sealed class Int8Translator : FixedTranslator<sbyte>
    {
        public static Int8Translator Default => new Int8Translator();

        public Int8Translator() : base(1, true) { }

        public override byte[] Serialize(sbyte obj) => new byte[1] { (byte)obj };
        public override sbyte Deserialize(byte[] data) => (sbyte)data[0];
    }

    public sealed class UInt16Translator : FixedTranslator<ushort>
    {
        public static UInt16Translator Default => new UInt16Translator();

        public UInt16Translator() : base(2, true) { }

        public override byte[] Serialize(ushort obj) => BitConverter.GetBytes(obj);
        public override ushort Deserialize(byte[] data) => BitConverter.ToUInt16(data);
    }

    public sealed class Int16Translator : FixedTranslator<short>
    {
        public static Int16Translator Default => new Int16Translator();

        public Int16Translator() : base(2, true) { }

        public override byte[] Serialize(short obj) => BitConverter.GetBytes(obj);
        public override short Deserialize(byte[] data) => BitConverter.ToInt16(data);
    }

    public sealed class UInt32Translator : FixedTranslator<uint>
    {
        public static UInt32Translator Default => new UInt32Translator();

        public UInt32Translator() : base(4, true) { }

        public override byte[] Serialize(uint obj) => BitConverter.GetBytes(obj);
        public override uint Deserialize(byte[] data) => BitConverter.ToUInt32(data);
    }

    public sealed class Int32Translator : FixedTranslator<int>
    {
        public static Int32Translator Default => new Int32Translator();

        public Int32Translator() : base(4, true) { }

        public override byte[] Serialize(int obj) => BitConverter.GetBytes(obj);
        public override int Deserialize(byte[] data) => BitConverter.ToInt32(data);
    }

    public sealed class UInt64Translator : FixedTranslator<ulong>
    {
        public static UInt64Translator Default => new UInt64Translator();

        public UInt64Translator() : base(8, true) { }

        public override byte[] Serialize(ulong obj) => BitConverter.GetBytes(obj);
        public override ulong Deserialize(byte[] data) => BitConverter.ToUInt64(data);
    }

    public sealed class Int64Translator : FixedTranslator<long>
    {
        public static Int64Translator Default => new Int64Translator();

        public Int64Translator() : base(8, true) { }

        public override byte[] Serialize(long obj) => BitConverter.GetBytes(obj);
        public override long Deserialize(byte[] data) => BitConverter.ToInt64(data);
    }

    public sealed class GuidTranslator : FixedTranslator<Guid>
    {
        public static GuidTranslator Default => new GuidTranslator();

        public GuidTranslator() : base(16, true) { }

        public override byte[] Serialize(Guid obj) => obj.ToByteArray();
        public override Guid Deserialize(byte[] data) => new Guid(data);
    }

    public sealed class BigIntTranslator : FixedTranslator<BigInteger>
    {
        public static BigIntTranslator Default => new BigIntTranslator();

        public BigIntTranslator() : base(16, false) { }

        public override byte[] Serialize(BigInteger obj) => obj.ToByteArray();
        public override BigInteger Deserialize(byte[] data) => new BigInteger(data);
    }

    public sealed class Float16Translator : FixedTranslator<Half>
    {
        public static Float16Translator Default => new Float16Translator();

        public Float16Translator() : base(2, true) { }

        public override byte[] Serialize(Half obj) => BitConverter.GetBytes(obj);
        public override Half Deserialize(byte[] data) => BitConverter.ToHalf(data);
    }

    public sealed class Float32Translator : FixedTranslator<float>
    {
        public static Float32Translator Default => new Float32Translator();

        public Float32Translator() : base(4, true) { }

        public override byte[] Serialize(float obj) => BitConverter.GetBytes(obj);
        public override float Deserialize(byte[] data) => BitConverter.ToSingle(data);
    }

    public sealed class Float64Translator : FixedTranslator<double>
    {
        public static Float64Translator Default => new Float64Translator();

        public Float64Translator() : base(8, true) { }

        public override byte[] Serialize(double obj) => BitConverter.GetBytes(obj);
        public override double Deserialize(byte[] data) => BitConverter.ToDouble(data);
    }

    public sealed class StringTranslator : FlexibleTranslator<string>
    {
        public static StringTranslator Default => new StringTranslator();

        public StringTranslator() : base(false) { }

        public override byte BytesPerElement => 2;

        public override byte[] Serialize(string obj, int length) => Encoding.Unicode.GetBytes(obj, 0, Math.Min(obj.Length, length)).EnsureSize(length);
        public override string Deserialize(byte[] data, int length) => Encoding.Unicode.GetString(data, 0, length);
    }

    public sealed class ASCIISTranslator : FlexibleTranslator<ASCIIS>
    {
        public static ASCIISTranslator Default => new ASCIISTranslator();

        public ASCIISTranslator() : base(false) { }

        public override byte BytesPerElement => 1;

        public override byte[] Serialize(ASCIIS obj, int length) => Encoding.ASCII.GetBytes(obj, 0, Math.Min(obj.Length, length)).EnsureSize(length);
        public override ASCIIS Deserialize(byte[] data, int length) => new ASCIIS(Encoding.ASCII.GetString(data, 0, length));
    }

    public sealed class ByteArrTranslator : FlexibleTranslator<byte[]>
    {
        public static ByteArrTranslator Default => new ByteArrTranslator();

        public ByteArrTranslator() : base(false) { }

        public override byte BytesPerElement => 1;

        public override byte[] Serialize(byte[] obj, int length) => obj.EnsureSize(length);
        public override byte[] Deserialize(byte[] data, int length) => data.EnsureSize(length);
    }

    #endregion
}