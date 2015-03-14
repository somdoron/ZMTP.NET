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
    public class Endpoint
    {
        public enum State
        {
            Idle,
            Connecting,
            SendingGreeting,
            ReceivingGreeting,
            SendingIdentity,
            ReceiveIdentitySize,
            ReceiveIdentity,
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

        public enum ReceiveState
        {
            Idle,
            Flag,
            OneByte,
            EightByte,
            Message,
            MessageReady,
        }

        public enum ReceiveAction
        {
            Start, 
            Received,
            ReceiveFailed,
            Fetched,
        }

        private readonly ConditionalVariable m_conditionalVariable;
        private readonly SocketType m_socketType;        

        private string m_hostName;
        private int m_port;

        private StateMachine<State, Action> m_stateMachine;
        private StateMachine<ReceiveState, ReceiveAction> m_receiveStateMachine;
        private StreamSocket m_streamSocket;

        private bool m_sending;        
        private Frame m_receivingFrame;
        
        public Endpoint(ConditionalVariable conditionalVariable, SocketType socketType, string address)
        {
            m_sending = false;
            
            m_conditionalVariable = conditionalVariable;
            m_socketType = socketType;

            Uri uri = new Uri(address);

            if (uri.Scheme != "tcp" || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.Query))
                throw new ArgumentException("invalid address");

            m_hostName = uri.Host;
            m_port = uri.Port;

            m_stateMachine = new StateMachine<State, Action>(m_conditionalVariable, State.Idle);

            m_stateMachine.On(State.Idle, Action.Start, () =>
            {
                m_streamSocket = new StreamSocket();

                m_stateMachine.State = State.Connecting;

                var asyncAction = m_streamSocket.ConnectAsync(new HostName(m_hostName), m_port.ToString());
                asyncAction.Completed = ConnectCompleted;
                
            });

            m_stateMachine.On(State.Connecting, Action.Connected, () =>
            {
                m_stateMachine.State = State.SendingGreeting;

                byte[] greeting = new byte[] { 0xFF, 0, 0, 0, 0, 0, 0, 0, 1, 0x7F, 0x01, (byte) m_socketType};

                SendData(greeting);                
            });

            m_stateMachine.On(State.SendingGreeting, Action.Sent, () =>
            {
                m_stateMachine.State = State.ReceivingGreeting;
                             
                ReceiveHandshakeData(12);              
            });

            m_stateMachine.On<IBuffer>(State.ReceivingGreeting, Action.Received, buffer =>
            {
                // TODO: Check the greeting is actually OK
                m_stateMachine.State = State.SendingIdentity;  
             
                byte[] identity = new byte[2] {0,0}; // Final and zero size
                
                SendData(identity);
            });

            m_stateMachine.On(State.SendingIdentity, Action.Sent, () =>
            {
                // receive identity size and flag
                m_stateMachine.State = State.ReceiveIdentitySize;

                ReceiveHandshakeData(2);
            });

            m_stateMachine.On<IBuffer>(State.ReceiveIdentitySize, Action.Received, buffer =>
            {
                var data = buffer.ToArray();

                Debug.Assert(data[0] == 0);

                if (data[1] == 0)
                {
                    m_stateMachine.State = State.Ready;

                    // ZMTP.NET ignore identities
                    m_receiveStateMachine.Feed(ReceiveAction.Start);

                    // let threads now that state has changed
                    m_conditionalVariable.PulseAll();  
                }
                else
                {
                    m_stateMachine.State = State.ReceiveIdentity;
                    ReceiveHandshakeData(data[1]);
                }
            });

            m_stateMachine.On<IBuffer>(State.ReceiveIdentity, Action.Received, buffer =>
            {
                m_stateMachine.State = State.Ready;

                // ZMTP.NET ignore identities
                m_receiveStateMachine.Feed(ReceiveAction.Start);

                // let threads now that state has changed
                m_conditionalVariable.PulseAll();  
            });

            m_stateMachine.On(State.Ready, Action.Sent, () =>
            {
                m_sending = false;

                m_conditionalVariable.PulseAll();  
            });

            m_receiveStateMachine = new StateMachine<ReceiveState, ReceiveAction>(m_conditionalVariable, ReceiveState.Idle);
            m_receiveStateMachine.On(ReceiveState.Idle, ReceiveAction.Start, () =>
            {
                m_receivingFrame = new Frame();

                m_receiveStateMachine.State = ReceiveState.Flag;
                ReceiveData(1);                                
            });

            m_receiveStateMachine.On(ReceiveState.MessageReady, ReceiveAction.Fetched, () =>
            {
                m_receiveStateMachine.State = ReceiveState.Flag;
                ReceiveData(1);                                
            });

            m_receiveStateMachine.On<IBuffer>(ReceiveState.Flag, ReceiveAction.Received, buffer =>
            {
                byte[] data = buffer.ToArray();
                int flag = data[0];
                                
                // TODO: more bit is not supported at the moment
                // m_receivingMessage.More = (flag & 1) != 0;
                Debug.Assert((flag & 1) == 0);

                bool eightSize = (flag & 2) != 0;

                if (eightSize)
                {
                    m_receiveStateMachine.State = ReceiveState.EightByte;
                    ReceiveData(8);                    
                }
                else
                {
                    m_receiveStateMachine.State = ReceiveState.OneByte;
                    ReceiveData(1);                    
                }
            });

            m_receiveStateMachine.On<IBuffer>(ReceiveState.OneByte, ReceiveAction.Received, buffer =>
            {
                byte[] data = buffer.ToArray();
                m_receivingFrame.Size = data[0];

                if (m_receivingFrame.Size > 0)
                {
                    m_receiveStateMachine.State = ReceiveState.Message;
                    ReceiveData(m_receivingFrame.Size);                    
                }
                else
                {
                    m_receivingFrame.Data = new byte[0];

                    m_receiveStateMachine.State = ReceiveState.MessageReady;

                    m_conditionalVariable.PulseAll();                
                }
            });

            m_receiveStateMachine.On<IBuffer>(ReceiveState.EightByte, ReceiveAction.Received, buffer =>
            {
                byte[] data = buffer.ToArray();
                m_receivingFrame.Size = (int)NetworkOrderBitsConverter.ToInt64(data);

                m_receiveStateMachine.State = ReceiveState.Message;
                ReceiveData(m_receivingFrame.Size);                
            });

            m_receiveStateMachine.On<IBuffer>(ReceiveState.Message, ReceiveAction.Received, buffer =>
            {
                byte[] data = buffer.ToArray();
                m_receivingFrame.Data = data;

                m_receiveStateMachine.State = ReceiveState.MessageReady;

                // let threads now that state has changed
                m_conditionalVariable.PulseAll();                
            });
        }
        
        public void Start()
        {
            m_stateMachine.Feed(Action.Start);
        }

        public bool TrySend(ref Frame frame)
        {
            // endpoint is not connected or socket is already in sending operation
            if (m_stateMachine.State != State.Ready || m_sending)
                return false;
            
            int payloadIndex;
            
            byte[] data;

            if (frame.Size <= 255)
            {
                data = new byte[2 + frame.Size];
                data[0] = 0; // More is not supported
                data[1] = (byte) frame.Size;
                
                payloadIndex = 2;
            }
            else
            {
                data = new byte[9 + frame.Size];
                data[0] =  2; // More is not supported

                NetworkOrderBitsConverter.PutInt64((long)frame.Size, data, 1);
                payloadIndex = 9;
            }

            frame.CopyTo(data, payloadIndex);

            m_sending = true;
            SendData(data);

            return true;
        }

        public bool TryReceive(ref Frame frame)
        {
            if (m_receiveStateMachine.State != ReceiveState.MessageReady)
                return false;

            frame = m_receivingFrame;
            m_receivingFrame = new Frame();

            if (m_stateMachine.State == State.Ready)
                m_receiveStateMachine.Feed(ReceiveAction.Fetched);

            return true;
        }

        private void ConnectCompleted(IAsyncAction asyncinfo, AsyncStatus asyncstatus)
        {
            if (asyncstatus == AsyncStatus.Completed)
            {
                m_stateMachine.Feed(Action.Connected);
            }
            else
            {
                m_stateMachine.Feed(Action.ConnectFailed);
            }
        }

        private void SendCompleted(IAsyncOperationWithProgress<uint, uint> asyncinfo, AsyncStatus status)
        {
            if (status == AsyncStatus.Completed)
            {
                m_stateMachine.Feed(Action.Sent);
            }
            else
            {
                m_stateMachine.Feed(Action.SendFailed);
            }
        }

        private void ReceiveHandshakeCompleted(IAsyncOperationWithProgress<IBuffer, uint> info, AsyncStatus status)
        {
            if (status == AsyncStatus.Completed)
            {                
                m_stateMachine.Feed(Action.Received, info.GetResults());
            }
            else
            {
                m_stateMachine.Feed(Action.ReceiveFailed);
            }
        }

        private void ReceiveMessageCompleted(IAsyncOperationWithProgress<IBuffer, uint> info, AsyncStatus status)
        {
            if (status == AsyncStatus.Completed)
            {
                m_receiveStateMachine.Feed(ReceiveAction.Received, info.GetResults());
            }
            else
            {
                m_receiveStateMachine.Feed(ReceiveAction.ReceiveFailed);
            }
        }

        private void SendData(byte[] data)
        {
            var asyncAction = m_streamSocket.OutputStream.WriteAsync(data.AsBuffer());
            asyncAction.Completed = SendCompleted;
        }

        private void ReceiveHandshakeData(int size)
        {
            var asyncAction = m_streamSocket.InputStream.ReadAsync(new Buffer((uint)size),(uint) size, InputStreamOptions.None);
            asyncAction.Completed = ReceiveHandshakeCompleted;  
        }

        private void ReceiveData(int size)
        {
            var asyncAction = m_streamSocket.InputStream.ReadAsync(new Buffer((uint)size), (uint)size, InputStreamOptions.ReadAhead);
            asyncAction.Completed = ReceiveMessageCompleted;
        }
    }
}
