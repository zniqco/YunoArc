using System;
using System.IO;
using System.Text;

namespace YunoArc
{
    public static class BinaryExtension
    {
        public static string ReadStringEncrypted(this BinaryReader reader, int length)
        {
            var position = reader.BaseStream.Position;
            var data = reader.ReadBytes(length);

            for (var i = 0; i < data.Length; ++i)
            {
                data[i] = Decrypt(data[i], position + i);

                if (data[i] == 0)
                    return Encoding.ASCII.GetString(data, 0, i);
            }

            return Encoding.ASCII.GetString(data, 0, data.Length);
        }

        public static ushort ReadUInt16Encrypted(this BinaryReader reader)
        {
            var position = reader.BaseStream.Position;
            var byte1 = Decrypt(reader.ReadByte(), position);
            var byte2 = Decrypt(reader.ReadByte(), position + 1);

            return (ushort)((byte2 << 8) | byte1);
        }

        public static void WriteStringEncrypted(this BinaryWriter writer, string text, int length)
        {
            var position = writer.BaseStream.Position;
            var data = new byte[length];
            var bytes = Encoding.ASCII.GetBytes(text);

            Array.Copy(bytes, data, bytes.Length);

            for (var i = 0; i < data.Length; ++i)
                data[i] = Encrypt(data[i], position + i);

            writer.Write(data);
        }

        public static void WriteUInt16Encrypted(this BinaryWriter writer, ushort value)
        {
            var position = writer.BaseStream.Position;
            var byte1 = Encrypt((byte)(value & 0xFF), position);
            var byte2 = Encrypt((byte)((value >> 8) & 0xFF), position + 1);

            writer.Write(byte1);
            writer.Write(byte2);
        }

        private static byte Decrypt(byte value, long position)
        {
            value = (byte)((value >> 1) | (value << 7));
            value ^= (byte)((position + 0x51) & 0xFF);

            return value;
        }

        private static byte Encrypt(byte value, long position)
        {
            value ^= (byte)((position + 0x51) & 0xFF);
            value = (byte)((value << 1) | (value >> 7));

            return value;
        }
    }
}
