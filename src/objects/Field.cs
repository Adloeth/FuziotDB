using System;
using System.Collections.Generic;

namespace FuziotDB
{
    internal class Field
    {
        private ASCIIS name;
        private ushort size;
        private TranslatorBase converter;

        public Field(ASCIIS name, ushort size, TranslatorBase converter)
        {
            this.name = name;
            this.size = size;
            this.converter = converter;
        }

        public ASCIIS Name => name;
        public ushort Size => size;
        public TranslatorBase Converter => converter;

        public static Field[] FromHeader(byte[] header)
        {
            int count = BitConverter.ToUInt16(header.Extract(0, 2).ToCurrentEndian(true)) + 1;
            List<Field> fields = new List<Field>(count);

            int offset = 2;
            for(int i = 0; i < count; i++)
            {
                offset = GetField(header, offset, out Field field);
                fields.Add(field);
            }

            return fields.ToArray();
        }

        private static int GetField(byte[] header, int offset, out Field field)
        {
            int nameSize = header[offset] + 1;
            byte[] nameBytes = header.Extract(offset + 1, nameSize);
            ushort size = BitConverter.ToUInt16(header.Extract(offset + 1 + nameSize, 2).ToCurrentEndian(true));
            field = new Field(new ASCIIS(nameBytes), size, null);
            return offset + 1 + nameSize + 2;
        }

        public byte[] CalcHeader()
        {
            byte[] result = new byte[1 + name.Length + 2];
            result[0] = (byte)(name.Length - 1);
            for(int i = 0 ; i < name.Length; i++)
                result[1 + i] = name[i];

            byte[] sizeBytes = BitConverter.GetBytes(size).ToLittleEndian();
            result[1 + name.Length] = sizeBytes[0];
            result[1 + name.Length + 1] = sizeBytes[1];

            return result;
        }

        public override bool Equals(object obj)
        {            
            if (obj == null || !(obj is Field field))
                return false;
                
            return field.name == name && field.size == size;
        }
        
        public override int GetHashCode() => HashCode.Combine(name, size);

        public static bool operator ==(Field a, Field b) => a.Equals(b);
        public static bool operator !=(Field a, Field b) => !a.Equals(b);

        public override string ToString()
            => string.Concat("'", name, "', ", size);
    }
}