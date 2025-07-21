using NetWorkLibrary.Algorithm;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace NetWorkLibrary.Utility
{
    public sealed class ByteBuffer
    {
        private List<byte> buffer;
        private int rpos, wpos;

        public int ReadPosition => rpos;

        public int WritePosition => wpos;

        public int Length => buffer.Count;

        public byte[] Data => buffer.ToArray();

        public ByteBuffer()
        {
            buffer = new List<byte>();
            rpos = wpos = 0;
        }

        public ByteBuffer(byte[] data)
        {
            buffer = new List<byte>();
            rpos = wpos = 0;
            Write(data);
        }

        public ByteBuffer(ByteBuffer byteBuffer)
        {
            buffer = new List<byte>();
            rpos = wpos = 0;
            Write(byteBuffer.Data);
        }

        public void ResetRead()
        {
            rpos = 0;
        }

        public void RemoveRead(int length = 0)
        {
            int remove = length == 0 ? rpos : length;
            buffer.RemoveRange(0, remove);
            wpos = wpos >= remove ? wpos - remove : 0;
            rpos = rpos >= remove ? rpos - remove : 0;
        }

        public void RemoveRead()
        {
            buffer.RemoveRange(0, rpos);
            wpos = wpos >= rpos ? wpos - rpos : 0;
            rpos = 0;
        }

        public void Write(SocketAsyncEventArgs args, IEncrypt encrypt = null)
        {
            var bytes = new byte[args.BytesTransferred];
            Array.Copy(args.Buffer, args.Offset, bytes, 0, bytes.Length);
            if (encrypt != null)
                bytes = encrypt.Encrypt(bytes);
            Write(bytes);
        }

        public void Write(byte[] data)
        {
            buffer.AddRange(data);
            wpos += data.Length;
        }

        public void WriteBoolean(bool value)
        {
            if (value)
                buffer.Add(1);
            else
                buffer.Add(0);
            wpos++;
        }

        public void WriteInt8(sbyte value)
        {
            WriteByte((byte)value);
        }

        public void WriteByte(byte value)
        {
            buffer.Add(value);
            wpos++;
        }

        public void WriteInt16(short value)
        {
            var data = BitConverter.GetBytes(value);
            WriteNumber(data);
        }

        public void WriteInt32(int value)
        {
            var data = BitConverter.GetBytes(value);
            WriteNumber(data);
        }

        public void WriteInt64(long value)
        {
            var data = BitConverter.GetBytes(value);
            WriteNumber(data);
        }

        public void WriteUint16(ushort value)
        {
            var data = BitConverter.GetBytes(value);
            WriteNumber(data);
        }

        public void WriteUint32(uint value)
        {
            var data = BitConverter.GetBytes(value);
            WriteNumber(data);
        }

        public void WriteUint64(ulong value)
        {
            var data = BitConverter.GetBytes(value);
            WriteNumber(data);
        }

        public void WriteSingle(float value)
        {
            var data = BitConverter.GetBytes(value);
            WriteNumber(data);
        }

        public void WriteDouble(double value)
        {
            var data = BitConverter.GetBytes(value);
            WriteNumber(data);
        }

        public void WriteString(string value)
        {
            byte[] data;
            if(string.IsNullOrEmpty(value))
            {
                data = Array.Empty<byte>();
            }
            else
            {
                data = Encoding.UTF8.GetBytes(value);
            }
            WriteInt32(data.Length);
            Write(data);
        }

        public void WriteEncodeString(string value, Encoding encoding)
        {
            byte[] data;
            if (string.IsNullOrEmpty(value))
            {
                data = Array.Empty<byte>();
            }
            else
            {
                data = encoding.GetBytes(value);
            }
            WriteInt32(data.Length);
            Write(data);
        }

        public byte[] ReadBytes(int length)
        {
            var data = new byte[length];
            buffer.CopyTo(rpos, data, 0, length);
            rpos += length;
            return data;
        }

        public bool ReadBoolean()
        {
            var value = ReadByte();
            return value == 1;
        }

        public sbyte ReadInt8()
        {
            var value = buffer[rpos];
            rpos++;
            return (sbyte)value;
        }

        public byte ReadByte()
        {
            var value = buffer[rpos];
            rpos++;
            return value;
        }

        public short ReadInt16()
        {
            var data = ReadNumber(2);
            return BitConverter.ToInt16(data, 0);
        }

        public int ReadInt32()
        {
            var data = ReadNumber(4);
            return BitConverter.ToInt32(data, 0);
        }

        public long ReadInt64()
        {
            var data = ReadNumber(8);
            return BitConverter.ToInt64(data, 0);
        }

        public ushort ReadUint16()
        {
            var data = ReadNumber(2);
            return BitConverter.ToUInt16(data, 0);
        }

        public uint ReadUint32()
        {
            var data = ReadNumber(4);
            return BitConverter.ToUInt32(data, 0);
        }

        public ulong ReadUint64()
        {
            var data = ReadNumber(8);
            return BitConverter.ToUInt64(data, 0);
        }

        public float ReadSingle()
        {
            var data = ReadNumber(4);
            return BitConverter.ToSingle(data, 0);
        }

        public double ReadDouble()
        {
            var data = ReadNumber(8);
            return BitConverter.ToDouble(data, 0);
        }

        public string ReadString()
        {
            var length = ReadInt32();
            var data = ReadBytes(length);
            return Encoding.UTF8.GetString(data);
        }

        public string ReadEncodeString(Encoding encoding)
        {
            var length = ReadInt32();
            var data = ReadBytes(length);
            return encoding.GetString(data);
        }

        private byte[] ReadNumber(int length)
        {
            var data = ReadBytes(length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data);

            return data;
        }

        private void WriteNumber(byte[] data)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data);

            Write(data);
        }
    }
}
