using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;

namespace ZMTP.NET
{
    class HandshakeStateMachine : StateMachine<HandshakeStateMachine.HandshakeState, HandshakeStateMachine.Action>
    {
        private StreamSocket m_streamSocket;
        private readonly string m_hostName;
        private readonly int m_port;
        private readonly SocketType m_socketType;

        public enum HandshakeState
        {
            Idle,
            Connecting,
            SendingGreeting,
            ReceivingGreeting,
            SendingIdentity,
            ReceivingIdentitySize,
            ReceivingIdentity,
            Ready,                       
        }

        public enum Action
        {
            Start,
            Connected,
            ConnectFailed,
            Sent,
            SendFailed,
            Received,
            ReceiveFailed
        }

        public HandshakeStateMachine(Context context, string hostName, int port, SocketType socketType)
            : base(context, HandshakeState.Idle)
        {            
            m_hostName = hostName;
            m_port = port;
            m_socketType = socketType;

            On(HandshakeState.Idle, Action.Start, () =>
            {
                State = HandshakeState.Connecting;

                var asyncAction = m_streamSocket.ConnectAsync(new HostName(m_hostName), m_port.ToString());
                asyncAction.Completed = ConnectCompleted;
            });

            On(HandshakeState.Connecting, Action.Connected, () =>
            {
                State = HandshakeState.SendingGreeting;

                byte[] greeting = new byte[] { 0xFF, 0, 0, 0, 0, 0, 0, 0, 1, 0x7F, 0x01, (byte)m_socketType };

                SendData(greeting);
            });

            On(HandshakeState.SendingGreeting, Action.Sent, () =>
            {
                State = HandshakeState.ReceivingGreeting;

                ReceiveHandshakeData(12);
            });

            On<IBuffer>(HandshakeState.ReceivingGreeting, Action.Received, buffer =>
            {
                // TODO: Check the greeting is actually OK
                State = HandshakeState.SendingIdentity;

                byte[] identity = new byte[2] { 0, 0 }; // Final and zero size

                SendData(identity);
            });

            On(HandshakeState.SendingIdentity, Action.Sent, () =>
            {
                // receive identity size and flag
                State = HandshakeState.ReceivingIdentitySize;

                ReceiveHandshakeData(2);
            });

            On<IBuffer>(HandshakeState.ReceivingIdentitySize, Action.Received, buffer =>
            {
                var data = buffer.ToArray();

                Debug.Assert(data[0] == 0);

                if (data[1] == 0)
                {
                    State = HandshakeState.Ready;

                    FireEvent(Completed);
                }
                else
                {
                    State = HandshakeState.ReceivingIdentity;
                    ReceiveHandshakeData(data[1]);
                }
            });

            On<IBuffer>(HandshakeState.ReceivingIdentity, Action.Received, buffer =>
            {
                State = HandshakeState.Ready;

                FireEvent(Completed);
            });          
        }

        public event EventHandler Completed;

        public void Start(StreamSocket streamSocket)
        {
            m_streamSocket = streamSocket;
            Handle(Action.Start);
        }

        private void ConnectCompleted(IAsyncAction asyncinfo, AsyncStatus asyncstatus)
        {
            if (asyncstatus == AsyncStatus.Completed)
            {
                Handle(Action.Connected);
            }
            else
            {
                Handle(Action.ConnectFailed);
            }
        }

        private void ReceiveHandshakeData(int size)
        {
            var asyncAction = m_streamSocket.InputStream.ReadAsync(new Buffer((uint)size), (uint)size, InputStreamOptions.None);
            asyncAction.Completed = ReceiveHandshakeCompleted;
        }

        private void SendData(byte[] data)
        {
            var asyncAction = m_streamSocket.OutputStream.WriteAsync(data.AsBuffer());
            asyncAction.Completed = SendCompleted;
        }

        private void ReceiveHandshakeCompleted(IAsyncOperationWithProgress<IBuffer, uint> info, AsyncStatus status)
        {
            if (status == AsyncStatus.Completed)
            {
                Handle(Action.Received, info.GetResults());
            }
            else
            {
                Handle(Action.ReceiveFailed);
            }
        }

        private void SendCompleted(IAsyncOperationWithProgress<uint, uint> asyncinfo, AsyncStatus status)
        {
            if (status == AsyncStatus.Completed)
            {
                Handle(Action.Sent);
            }
            else
            {
                Handle(Action.SendFailed);
            }
        }
    }
}
