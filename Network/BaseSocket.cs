using NetWorkLibrary.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace NetWorkLibrary.Network
{
    public interface ISocket
    {
        /// <summary>
        /// 打开
        /// </summary>
        void Open();

        /// <summary>
        /// 更新
        /// </summary>
        bool Update();

        /// <summary>
        /// 是否处于打开状态
        /// </summary>
        bool IsOpen();

        /// <summary>
        /// 关闭
        /// </summary>
        void Close();
    }

    public interface IPacket
    {
        uint PacketOpcode { get; }

        /// <summary>
        /// 解包
        /// </summary>
        void Read(ByteBuffer buffer);

        /// <summary>
        /// 封包
        /// </summary>
        void Write(ByteBuffer buffer);

        /// <summary>
        /// 获取包命令
        /// </summary>
        uint GetOpcode(ByteBuffer buffer);

        /// <summary>
        /// 获取包长
        /// </summary>
        uint GetLength(ByteBuffer buffer);
    }

    /// <summary>
    /// 消息处理函数
    /// </summary>
    public delegate void PacketHandlerAction(ISocket socket, ByteBuffer buffer);

    public abstract class BaseSocket<TBasePacket> : ISocket, IDisposable where TBasePacket : IPacket
    {
        private Socket linkSocket;

        private IPEndPoint linkIP;

        private SocketAsyncEventArgs readArgs;

        protected ByteBuffer ReadBuffer;

        protected readonly int MaxBuffSize = 0x4000;
        protected readonly int MaxPacketBuffSize = 100 * 1024 * 1024;

        protected BaseSocket(Socket socket)
        {
            RegisterPacketHandlers();

            linkSocket = socket;
            linkIP = (IPEndPoint)socket.RemoteEndPoint;

            ReadBuffer = new ByteBuffer();
            readArgs = new SocketAsyncEventArgs();
            readArgs.SetBuffer(new byte[MaxBuffSize], 0, MaxBuffSize);
            readArgs.Completed += (sender, args) => { ProcessReadAsync(args); };

            ProcessRead();
        }

        #region ISocket implements

        public abstract void Open();

        public virtual bool Update()
        {
            if (!IsOpen())
                return false;

            return true;
        }

        public bool IsOpen()
        {
            return linkSocket != null && linkSocket.Connected;
        }

        public void Close()
        {
            if (linkSocket == null || !linkSocket.Connected)
                return;

            try
            {
                linkSocket.Shutdown(SocketShutdown.Both);
                linkSocket.Close();
            }
            catch (Exception e)
            {
                LogManager.Instance.Log(LogType.Error, $"BaseSocket.Close : {linkIP} errored when shuntting down socket:{e.Message}");
            }

            OnClose();
        }

        #endregion

        #region IDisposable implements
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    linkSocket.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~BaseSocket()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        protected static Dictionary<uint, PacketHandlerAction> Handlers = new Dictionary<uint, PacketHandlerAction>();
        public static void RegisterPacketHandler(uint opcode, PacketHandlerAction handler)
        {
            if (Handlers.ContainsKey(opcode))
                Handlers[opcode] = handler;
            else
                Handlers.Add(opcode, handler);
        }

        private static bool Registered = false;
        private static void RegisterPacketHandlers()
        {
            if (!Registered)
            {
                var typs = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(x => x.GetTypes())
                    .Where(x => x.IsSubclassOf(typeof(PacketHandler<TBasePacket>)) && !x.IsAbstract);
                foreach (var t in typs)
                {
                    Activator.CreateInstance(t, true);
                }
                Registered = true;
            }
        }

        private void ProcessReadAsync(SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success)
            {
                Close();
                return;
            }

            if (args.BytesTransferred == 0)
            {
                Close();
                return;
            }

            ProcessPacket(args);
            ProcessRead();
        }

        private void ProcessRead()
        {
            if (!IsOpen())
                return;

            readArgs.SetBuffer(0, MaxBuffSize);
            if (!linkSocket.ReceiveAsync(readArgs))
                ProcessReadAsync(readArgs);
        }

        public virtual void OnClose()
        {
            Dispose();
        }

        protected virtual void ProcessPacket(SocketAsyncEventArgs args)
        {
            ReadBuffer.Write(args);

            while (ReadBuffer.Length > sizeof(uint))
            {
                try
                {
                    TBasePacket packet = Activator.CreateInstance<TBasePacket>();
                    uint cmd = packet.GetOpcode(ReadBuffer);
                    uint length = packet.GetLength(ReadBuffer);

                    if (MaxPacketBuffSize < length)
                    {
                        LogManager.Instance.Log(LogType.Warning, $"[{linkIP}] send to larger packet[{cmd}] with length[{length}], closed.");
                        Close();
                        return;
                    }

                    if (ReadBuffer.Length < length)
                    {
                        ReadBuffer.ResetRead();
                        return;
                    }

                    var bytes = ReadBuffer.ReadBytes((int)length);
                    if (Handlers.ContainsKey(cmd))
                    {
                        ByteBuffer buffer = new ByteBuffer(bytes);
                        Handlers[cmd].Invoke(this, buffer);
                    }
                    else
                    {
                        LogManager.Instance.Log(LogType.Warning, $"[{linkIP}] send unknown packet[{cmd}] with length[{length}].");
                    }
                }
                catch { }

                ReadBuffer.RemoveRead();
            }
        }

        public void ProcessWrite(byte[] data)
        {
            if (!IsOpen())
                return;

            linkSocket.Send(data);
        }

        public void SetNoDelay(bool enable)
        {
            linkSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, enable);
        }

        public IPEndPoint LinkIP => linkIP;
    }
}
