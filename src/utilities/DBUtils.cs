using System;
using System.Text;
using System.Runtime.InteropServices;

namespace FuziotDB
{
    internal static class DBUtils
    {
        public static byte[] ToLittleEndian(this byte[] bytes)
        {
            if(BitConverter.IsLittleEndian)
                return bytes;

            Array.Reverse(bytes);
            return bytes;
        }

        public static byte[] ToBigEndian(this byte[] bytes)
        {
            if(BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return bytes;
        }

        /// <summary>
        /// This function will make the byte array to be the same endianness as the system's. But you need to say which endianness your byte array is. 
        /// If you assume your byte array to be big endian, set 'arrayEndian' parameter to false, otherwise set it to true.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="arrayEndian">False if the byte array is big endian, true otherwise.</param>
        /// <returns></returns>
        public static byte[] ToCurrentEndian(this byte[] bytes, bool arrayEndian)
        {
            if(BitConverter.IsLittleEndian != arrayEndian)
                Array.Reverse(bytes);

            return bytes;
        }

        public static void ToLittleEndian(ref byte[] bytes)
        {
            if(BitConverter.IsLittleEndian)
                return;

            Array.Reverse(bytes);
        }

        public static void ToBigEndian(ref byte[] bytes)
        {
            if(BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
        }

        /// <summary>
        /// This function will make the byte array to be the same endianness as the system's. But you need to say which endianness your byte array is. 
        /// If you assume your byte array to be big endian, set 'arrayEndian' parameter to false, otherwise set it to true.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="arrayEndian">False if the byte array is big endian, true otherwise.</param>
        /// <returns></returns>
        public static void ToCurrentEndian(ref byte[] bytes, bool arrayEndian)
        {
            if(BitConverter.IsLittleEndian != arrayEndian)
                Array.Reverse(bytes);
        }

        public static string PascalToSnake(this string str)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(str.Length * 2);
            int successiveUpper = 0;
            
            if(char.IsUpper(str[0])) sb.Append(char.ToLower(str[0]));
            else                     sb.Append(str[0]);

            for (int i = 1; i < str.Length; i++)
            {
                if(char.IsWhiteSpace(str[i]) || str[i] == '.')
                    continue;

                if(char.IsUpper(str[i]))
                {
                    if(successiveUpper < 1)
                        sb.Append('_');
                    sb.Append(char.ToLower(str[i]));
                    successiveUpper++;
                }
                else
                {
                    if(successiveUpper > 1)
                        sb.Append('_');
                    successiveUpper = 0;
                    sb.Append(str[i]);
                }
            }

            return sb.ToString();
        }

        public static bool IsASCIICompatible(this string str)
        {
            for(int i = 0; i < str.Length; i++)
                if(str[i] > 127)
                    return false;

            return true;
        }

        public static byte[] Extract(this byte[] buffer, int from, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(buffer, from, result, 0, length);
            return result;
        }

        public static byte[] Extract(this byte[] buffer, long from, long length)
        {
            byte[] result = new byte[length];
            Array.Copy(buffer, from, result, 0, length);
            return result;
        }

        private static readonly byte[] BitCountLookup = new byte[16]
        {
            0, 1, 1, 2, 1, 2, 2, 3,
            1, 2, 2, 3, 2, 3, 3, 4
        };

        public static int CountBits(this byte value) => BitCountLookup[value & 0x0F] + BitCountLookup[value >> 4];

        public static unsafe byte[] PtrToArray(byte* ptr, int length)
        {
            byte[] result = new byte[length];
            Marshal.Copy((nint)ptr, result, 0, length);
            return result;
        }

        public static byte[] EnsureSize(this byte[] source, int length)
        {
            if(source.Length == length) return source;

            byte[] result = new byte[length];
            Array.Copy(source, result, Math.Min(source.Length, length));
            return result;
        }
    }
}