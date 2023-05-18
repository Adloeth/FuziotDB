using BigInt = System.Numerics.BigInteger;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

namespace FuziotDB
{
    /// <summary>
    /// Handles data across the database API. Because only certain types are authorized, mainly primitive types.
    /// The only big disavantage is the time lost by boxing/unboxing all these value types.
    /// </summary>
    public struct DBVariant
    {
        private object var;
        private DBType type;
        // From 1 to 65 536 (substract by 1 before setting this value)
        private ushort size;

        public int Size { get => (int)(size + 1); set => size = (ushort)(value - 1); }
        public ushort StoredSize => size;

        private DBVariant(object var, DBType type, ushort size)
        {
            this.var = var;
            this.type = type;
            this.size = size;
        }

        public static bool IsSupported<T>() => IsSupported(typeof(T));
        public static bool IsSupported(Type type) => 
            type == typeof(   bool)
         || type == typeof(   byte)
         || type == typeof(  sbyte)
         || type == typeof( ushort)
         || type == typeof(  short)
         || type == typeof(   uint)
         || type == typeof(    int)
         || type == typeof(  ulong)
         || type == typeof(   long)
         || type == typeof(   Guid)
         || type == typeof( BigInt)
         || type == typeof(VarUInt)
         || type == typeof( string)
         || type == typeof( ASCIIS)
         || type == typeof(   Blob)
         || type == typeof(   Half)
         || type == typeof(  float)
         || type == typeof( double);

        public static int GetSize(Type type)
        {
            if(type == typeof(   bool)) return 1;
            if(type == typeof(   byte)) return 1;
            if(type == typeof(  sbyte)) return 1;
            if(type == typeof( ushort)) return 2;
            if(type == typeof(  short)) return 2;
            if(type == typeof(   uint)) return 4;
            if(type == typeof(    int)) return 4;
            if(type == typeof(  ulong)) return 8;
            if(type == typeof(   long)) return 8;
            if(type == typeof(   Guid)) return 16;
            if(type == typeof( BigInt)) return 16;
            if(type == typeof(   Half)) return 2;
            if(type == typeof(  float)) return 4;
            if(type == typeof( double)) return 8;
            return -1;
        }

        public static DBType GetDBType(Type type)
        {
            if(type == typeof(   bool)) return DBType.UInt;
            if(type == typeof(   byte)) return DBType.UInt;
            if(type == typeof( ushort)) return DBType.UInt;
            if(type == typeof(   uint)) return DBType.UInt;
            if(type == typeof(  ulong)) return DBType.UInt;
            if(type == typeof(   Guid)) return DBType.UInt;
            if(type == typeof(  sbyte)) return DBType.Int;
            if(type == typeof(  short)) return DBType.Int;
            if(type == typeof(    int)) return DBType.Int;
            if(type == typeof(   long)) return DBType.Int;
            if(type == typeof( BigInt)) return DBType.Int;
            if(type == typeof( string)) return DBType.UTF16;
            if(type == typeof( ASCIIS)) return DBType.ASCII;
            if(type == typeof(   Half)) return DBType.Float;
            if(type == typeof(  float)) return DBType.Float;
            if(type == typeof( double)) return DBType.Float;
            if(type == typeof(VarUInt)) return DBType.Variable;
            if(type == typeof(   Blob)) return DBType.Variable;
            throw new Exception("Invalid Type");
        }

        public static DBVariant FromObject(object obj, int length) => FromObject(obj.GetType(), obj, length);
        public static DBVariant FromObject(Type type, object obj, int length)
        {
            if(type == typeof(   bool)) return new DBVariant((   bool)obj);
            if(type == typeof(   byte)) return new DBVariant((   byte)obj);
            if(type == typeof( ushort)) return new DBVariant(( ushort)obj);
            if(type == typeof(   uint)) return new DBVariant((   uint)obj);
            if(type == typeof(  ulong)) return new DBVariant((  ulong)obj);
            if(type == typeof(   Guid)) return new DBVariant((   Guid)obj);
            if(type == typeof(  sbyte)) return new DBVariant((  sbyte)obj);
            if(type == typeof(  short)) return new DBVariant((  short)obj);
            if(type == typeof(    int)) return new DBVariant((    int)obj);
            if(type == typeof(   long)) return new DBVariant((   long)obj);
            if(type == typeof( BigInt)) return new DBVariant(( BigInt)obj);
            if(type == typeof( string)) return new DBVariant(( string)obj, length);
            if(type == typeof( ASCIIS)) return new DBVariant(( ASCIIS)obj, length);
            if(type == typeof(   Half)) return new DBVariant((   Half)obj);
            if(type == typeof(  float)) return new DBVariant((  float)obj);
            if(type == typeof( double)) return new DBVariant(( double)obj);
            if(type == typeof(VarUInt)) return new DBVariant((VarUInt)obj);
            if(type == typeof(   Blob)) return new DBVariant((   Blob)obj, length);
            throw new Exception("Invalid Type");
        }

        public static DBVariant FromBytes(byte[] bytes, ushort size, DBType type)
        {
            bytes = bytes.ToCurrentEndian(true);

            if(type == DBType.UInt && size == 1 ) return new DBVariant(bytes[0]);
            if(type == DBType.UInt && size == 2 ) return new DBVariant(BitConverter.ToUInt16(bytes));
            if(type == DBType.UInt && size == 4 ) return new DBVariant(BitConverter.ToUInt32(bytes));
            if(type == DBType.UInt && size == 8 ) return new DBVariant(BitConverter.ToUInt64(bytes));
            if(type == DBType.UInt && size == 16) return new DBVariant(new Guid(bytes));
            if(type == DBType. Int && size == 1 ) return new DBVariant((sbyte)bytes[0]);
            if(type == DBType. Int && size == 2 ) return new DBVariant(BitConverter.ToInt16(bytes));
            if(type == DBType. Int && size == 4 ) return new DBVariant(BitConverter.ToInt32(bytes));
            if(type == DBType. Int && size == 8 ) return new DBVariant(BitConverter.ToInt64(bytes));
            if(type == DBType. Int && size == 16) return new DBVariant(new BigInt(bytes));
            if(type == DBType.UTF16) return new DBVariant(Encoding.Unicode.GetString(bytes), size);
            if(type == DBType.ASCII) return new DBVariant(Encoding.ASCII.GetString(bytes), size);
            if(type == DBType.Float && size == 2) return new DBVariant(BitConverter.ToHalf(bytes));
            if(type == DBType.Float && size == 4) return new DBVariant(BitConverter.ToSingle(bytes));
            if(type == DBType.Float && size == 8) return new DBVariant(BitConverter.ToDouble(bytes));
            if(type == DBType.Variable) return new DBVariant(new VarUInt(bytes));
            if(type == DBType.Variable) return new DBVariant(new Blob(bytes, size), size);
            throw new Exception(string.Concat("Invalid Type (", bytes.Length, ", ", size, ", ", type, ")"));
        }

        public static unsafe DBVariant FromObjectBytes(byte* bytes, long offset, ushort size, DBType type)
        {
            bytes += offset;

            if(type == DBType.UInt && size == 1 ) return new DBVariant(bytes[0]);
            if(type == DBType.UInt && size == 2 ) return new DBVariant(((ushort*)bytes)[0]);
            if(type == DBType.UInt && size == 4 ) return new DBVariant(((  uint*)bytes)[0]);
            if(type == DBType.UInt && size == 8 ) return new DBVariant((( ulong*)bytes)[0]);
            if(type == DBType.UInt && size == 16) return new DBVariant(((  Guid*)bytes)[0]);
            if(type == DBType. Int && size == 1 ) return new DBVariant((sbyte)bytes[0]);
            if(type == DBType. Int && size == 2 ) return new DBVariant((( short*)bytes)[0]);
            if(type == DBType. Int && size == 4 ) return new DBVariant(((   int*)bytes)[0]);
            if(type == DBType. Int && size == 8 ) return new DBVariant(((  long*)bytes)[0]);
            if(type == DBType. Int && size == 16) return new DBVariant(((BigInt*)bytes)[0]);
            if(type == DBType.UTF16) return new DBVariant(Encoding.Unicode.GetString(bytes, size), size);
            if(type == DBType.ASCII) return new DBVariant(Encoding.ASCII.GetString(bytes, size), size);
            if(type == DBType.Float && size == 2) return new DBVariant(((  Half*)bytes)[0]);
            if(type == DBType.Float && size == 4) return new DBVariant((( float*)bytes)[0]);
            if(type == DBType.Float && size == 8) return new DBVariant(((double*)bytes)[0]);
            //if(type == DBType.Variable) return new DBVariant(((VarUInt*)bytes)[0]);
            //if(type == DBType.Variable) return new DBVariant();
            //if(type == DBType.Variable) return new DBVariant(new Blob(bytes));
            throw new Exception(string.Concat("Invalid Type (", size, ", ", type, ")"));
        }

        public DBVariant(  bool val) : this(val, DBType.UInt, 0) { }

        public DBVariant(  byte val) : this(val, DBType.UInt, 0) { }
        public DBVariant(ushort val) : this(val, DBType.UInt, 1) { }
        public DBVariant(  uint val) : this(val, DBType.UInt, 3) { }
        public DBVariant( ulong val) : this(val, DBType.UInt, 7) { }

        public DBVariant( sbyte val) : this(val, DBType.Int , 0) { }
        public DBVariant( short val) : this(val, DBType.Int , 1) { }
        public DBVariant(   int val) : this(val, DBType.Int , 3) { }
        public DBVariant(  long val) : this(val, DBType.Int , 7) { }

        public DBVariant(  Half val) : this(val, DBType.Float , 1) { }
        public DBVariant( float val) : this(val, DBType.Float , 3) { }
        public DBVariant(double val) : this(val, DBType.Float , 7) { }

        public DBVariant(  Guid val) : this(val, DBType.UInt, 15) { }
        public DBVariant(BigInt val) : this(val, DBType.UInt, 15) { }

        public DBVariant(VarUInt val) : this(val, DBType.Variable, (ushort)(val.Size - 1)) { }

        public DBVariant(string val, int length) : this(val.Length > length ? val.Substring(0, length) : val, DBType.UTF16, (ushort)(length * 2 - 1)) { if(length > ushort.MaxValue / 2) throw new Exception(string.Concat("Cannot set UTF16 string as Variant because it is longer than ", ushort.MaxValue / 2, " chars.")); }
        public DBVariant(ASCIIS val, int length) : this(val.Length > length ? val.Substring(0, length) : val, DBType.ASCII, (ushort)(length - 1)) { if(length > ushort.MaxValue) throw new Exception(string.Concat("Cannot set ASCII string as Variant because it is longer than ", ushort.MaxValue, " chars.")); }
        public DBVariant(  Blob val, int length) : this(val, DBType.Variable, (ushort)(length - 1)) { if(length > ushort.MaxValue) throw new Exception(string.Concat("Cannot set blob as Variant because it is longer than ", ushort.MaxValue, " bytes.")); }

        public byte[] GetBytes()
        {
                 if(var is    bool v0 ) return BitConverter.GetBytes(v0);
            else if(var is    byte v1 ) return new byte[1] { v1 };
            else if(var is   sbyte v2 ) return new byte[1] { (byte)v2 };
            else if(var is  ushort v3 ) return BitConverter.GetBytes(v3 ).ToLittleEndian();
            else if(var is   short v4 ) return BitConverter.GetBytes(v4 ).ToLittleEndian();
            else if(var is    uint v5 ) return BitConverter.GetBytes(v5 ).ToLittleEndian();
            else if(var is     int v6 ) return BitConverter.GetBytes(v6 ).ToLittleEndian();
            else if(var is   ulong v7 ) return BitConverter.GetBytes(v7 ).ToLittleEndian();
            else if(var is    long v8 ) return BitConverter.GetBytes(v8 ).ToLittleEndian();
            else if(var is    Half v9 ) return BitConverter.GetBytes(v9 ).ToLittleEndian();
            else if(var is   float v10) return BitConverter.GetBytes(v10).ToLittleEndian();
            else if(var is  double v11) return BitConverter.GetBytes(v11).ToLittleEndian();
            else if(var is    Guid v12) return v12.ToByteArray().ToLittleEndian();
            else if(var is  BigInt v13) return v13.ToByteArray().ToLittleEndian();
            else if(var is  string v14) return Encoding.Unicode.GetBytes(v14);
            else if(var is  ASCIIS v15) return Encoding.ASCII  .GetBytes(v15);
            else if(var is    Blob v16) return v16;
            else if(var is VarUInt v17) return (byte[])v17;
            else throw new Exception(string.Concat("Invalid variant type '", var.GetType().FullName, "'."));
        }

        public static implicit operator    bool(DBVariant variant) => (   bool)variant.var;
        public static implicit operator    byte(DBVariant variant) => (   byte)variant.var;
        public static implicit operator  ushort(DBVariant variant) => ( ushort)variant.var;
        public static implicit operator    uint(DBVariant variant) => (   uint)variant.var;
        public static implicit operator   ulong(DBVariant variant) => (  ulong)variant.var;
        public static implicit operator   sbyte(DBVariant variant) => (  sbyte)variant.var;
        public static implicit operator   short(DBVariant variant) => (  short)variant.var;
        public static implicit operator     int(DBVariant variant) => (    int)variant.var;
        public static implicit operator    long(DBVariant variant) => (   long)variant.var;
        public static implicit operator    Half(DBVariant variant) => (   Half)variant.var;
        public static implicit operator   float(DBVariant variant) => (  float)variant.var;
        public static implicit operator  double(DBVariant variant) => ( double)variant.var;
        public static implicit operator    Guid(DBVariant variant) => (   Guid)variant.var;
        public static implicit operator  BigInt(DBVariant variant) => ( BigInt)variant.var;
        public static implicit operator VarUInt(DBVariant variant) => (VarUInt)variant.var;
        public static implicit operator  string(DBVariant variant) => ( string)variant.var;
        public static implicit operator  ASCIIS(DBVariant variant) => ( ASCIIS)variant.var;
        public static implicit operator    Blob(DBVariant variant) => (   Blob)variant.var;

        public override string ToString() => string.Concat("[Var: ", type, ", ", size + 1 ,"] ", var.ToString());
    }
}