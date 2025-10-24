using System;
using UsbSerialForAndroid.Net.Modbus.Enums;

namespace UsbSerialForAndroid.Net.Modbus.Extensions
{
    /// <summary>
    /// Byte order extension method
    /// </summary>
    public static class EndianFormatExtension
    {
        public static int ToInt32(this byte[] bytes, ByteFormat format)
        {
            ValidateTypeFromByteFormat(typeof(int), format);
            if (bytes.Length != 4)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length));
            return format switch
            {
                ByteFormat.ABCD => BitConverter.ToInt32(new byte[] { bytes[3], bytes[2], bytes[1], bytes[0] }, 0),
                ByteFormat.CDAB => BitConverter.ToInt32(new byte[] { bytes[1], bytes[0], bytes[3], bytes[2] }, 0),
                ByteFormat.BADC => BitConverter.ToInt32(new byte[] { bytes[2], bytes[3], bytes[0], bytes[1] }, 0),
                ByteFormat.DCBA => BitConverter.ToInt32(bytes, 0),
                _ => default,
            };
        }

        public static int[] ToInt32Array(this byte[] bytes, ByteFormat format)
        {
            if (bytes.Length < 4 || bytes.Length % 4 > 0)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length));
            int[] values = new int[bytes.Length / 4];
            for (int i = 0; i < bytes.Length; i += 4)
            {
                values[i / 4] = bytes.Slice(i, 4).ToInt32(format);
            }
            return values;
        }

        public static uint ToUInt32(this byte[] bytes, ByteFormat format)
        {
            ValidateTypeFromByteFormat(typeof(uint), format);
            if (bytes.Length != 4)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length));
            return format switch
            {
                ByteFormat.ABCD => BitConverter.ToUInt32(new byte[] { bytes[3], bytes[2], bytes[1], bytes[0] }, 0),
                ByteFormat.CDAB => BitConverter.ToUInt32(new byte[] { bytes[1], bytes[0], bytes[3], bytes[2] }, 0),
                ByteFormat.BADC => BitConverter.ToUInt32(new byte[] { bytes[2], bytes[3], bytes[0], bytes[1] }, 0),
                ByteFormat.DCBA => BitConverter.ToUInt32(bytes, 0),
                _ => default,
            };
        }

        public static uint[] ToUInt32Array(this byte[] bytes, ByteFormat format)
        {
            if (bytes.Length < 4 || bytes.Length % 4 > 0)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length));
            uint[] values = new uint[bytes.Length / 4];
            for (int i = 0; i < bytes.Length; i += 4)
            {
                values[i / 4] = bytes.Slice(i, 4).ToUInt32(format);
            }
            return values;
        }

        public static float ToFloat(this byte[] bytes, ByteFormat format)
        {
            ValidateTypeFromByteFormat(typeof(float), format);
            if (bytes.Length != 4)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length));
            return format switch
            {
                ByteFormat.ABCD => BitConverter.ToSingle(new byte[] { bytes[3], bytes[2], bytes[1], bytes[0] }, 0),
                ByteFormat.CDAB => BitConverter.ToSingle(new byte[] { bytes[1], bytes[0], bytes[3], bytes[2] }, 0),
                ByteFormat.BADC => BitConverter.ToSingle(new byte[] { bytes[2], bytes[3], bytes[0], bytes[1] }, 0),
                ByteFormat.DCBA => BitConverter.ToSingle(bytes, 0),
                _ => default,
            };
        }

        public static float[] ToFloatArray(this byte[] bytes, ByteFormat format)
        {
            if (bytes.Length < 4 || bytes.Length % 4 > 0)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length), bytes.Length, "The length of the array must be a multiple of 4");
            float[] values = new float[bytes.Length / 4];
            for (int i = 0; i < bytes.Length; i += 4)
            {
                values[i / 4] = bytes.Slice(i, 4).ToFloat(format);
            }
            return values;
        }

        public static double ToDouble(this byte[] bytes, ByteFormat format)
        {
            ValidateTypeFromByteFormat(typeof(double), format);
            if (bytes.Length != 8)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length));
            return format switch
            {
                ByteFormat.ABCDEFGH => BitConverter.ToDouble(new byte[] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] }, 0),
                ByteFormat.GHEFCDAB => BitConverter.ToDouble(new byte[] { bytes[1], bytes[0], bytes[3], bytes[2], bytes[5], bytes[4], bytes[7], bytes[6] }, 0),
                ByteFormat.BADCFEHG => BitConverter.ToDouble(new byte[] { bytes[6], bytes[7], bytes[4], bytes[5], bytes[2], bytes[3], bytes[0], bytes[1] }, 0),
                ByteFormat.HGFEDCBA => BitConverter.ToDouble(bytes, 0),
                _ => default,
            };
        }

        public static double[] ToDoubleArray(this byte[] bytes, ByteFormat format)
        {
            if (bytes.Length < 8 || bytes.Length % 8 > 0)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length), bytes.Length, "The length of the array must be a multiple of 8");
            double[] values = new double[bytes.Length / 8];
            for (int i = 0; i < bytes.Length; i += 8)
            {
                values[i / 8] = bytes.Slice(i, 8).ToDouble(format);
            }
            return values;
        }

        public static long ToInt64(this byte[] bytes, ByteFormat format)
        {
            ValidateTypeFromByteFormat(typeof(long), format);
            if (bytes.Length != 8)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length));
            return format switch
            {
                ByteFormat.ABCDEFGH => BitConverter.ToInt64(new byte[] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] }, 0),
                ByteFormat.GHEFCDAB => BitConverter.ToInt64(new byte[] { bytes[1], bytes[0], bytes[3], bytes[2], bytes[5], bytes[4], bytes[7], bytes[6] }, 0),
                ByteFormat.BADCFEHG => BitConverter.ToInt64(new byte[] { bytes[6], bytes[7], bytes[4], bytes[5], bytes[2], bytes[3], bytes[0], bytes[1] }, 0),
                ByteFormat.HGFEDCBA => BitConverter.ToInt64(bytes, 0),
                _ => default,
            };
        }

        public static long[] ToInt64Array(this byte[] bytes, ByteFormat format)
        {
            if (bytes.Length < 8 || bytes.Length % 8 > 0)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length), bytes.Length, "The length of the array must be a multiple of 8");
            long[] values = new long[bytes.Length / 8];
            for (int i = 0; i < bytes.Length; i += 8)
            {
                values[i / 8] = bytes.Slice(i, 8).ToInt64(format);
            }
            return values;
        }

        public static ulong ToUInt64(this byte[] bytes, ByteFormat format)
        {
            ValidateTypeFromByteFormat(typeof(ulong), format);
            if (bytes.Length != 8)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length));
            return format switch
            {
                ByteFormat.ABCDEFGH => BitConverter.ToUInt64(new byte[] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] }, 0),
                ByteFormat.GHEFCDAB => BitConverter.ToUInt64(new byte[] { bytes[1], bytes[0], bytes[3], bytes[2], bytes[5], bytes[4], bytes[7], bytes[6] }, 0),
                ByteFormat.BADCFEHG => BitConverter.ToUInt64(new byte[] { bytes[6], bytes[7], bytes[4], bytes[5], bytes[2], bytes[3], bytes[0], bytes[1] }, 0),
                ByteFormat.HGFEDCBA => BitConverter.ToUInt64(bytes, 0),
                _ => default,
            };
        }

        public static ulong[] ToUInt64Array(this byte[] bytes, ByteFormat format)
        {
            if (bytes.Length < 8 || bytes.Length % 8 > 0)
                throw new ArgumentOutOfRangeException(nameof(bytes.Length), bytes.Length, "The length of the array must be a multiple of 8");
            ulong[] values = new ulong[bytes.Length / 8];
            for (int i = 0; i < bytes.Length; i += 8)
            {
                values[i / 8] = bytes.Slice(i, 8).ToUInt64(format);
            }
            return values;
        }

        private static void ValidateTypeFromByteFormat(Type type, ByteFormat format)
        {
            var name = type.FullName;
            switch (format)
            {
                case ByteFormat.ABCD:
                case ByteFormat.CDAB:
                case ByteFormat.BADC:
                case ByteFormat.DCBA:
                    if (name != typeof(int).FullName && name != typeof(uint).FullName && name != typeof(float).FullName)
                        throw new ArgumentException("Type error. Only 4-byte basic data types are supported", nameof(type));
                    break;
                case ByteFormat.ABCDEFGH:
                case ByteFormat.GHEFCDAB:
                case ByteFormat.BADCFEHG:
                case ByteFormat.HGFEDCBA:
                    if (name != typeof(long).FullName && name != typeof(ulong).FullName && name != typeof(double).FullName)
                        throw new ArgumentException("Type error. Only 8-byte basic data types are supported", nameof(type));
                    break;
                default:
                    throw new Exception($"Unsupported byte format `{format}` `{name}`");
            }
        }
    }
}
