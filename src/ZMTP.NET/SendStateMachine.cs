using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Networking.Sockets;

namespace ZMTP.NET
{
    class SendStateMachine : StateMachine<SendStateMachine.SendState, SendStateMachine.Action>
    {
        private StreamSocket m_streamSocket;

        public enum SendState
        {
            Idle, Ready, Sending
        }

        public enum Action
        {
            Start,  Sent, SendFailed
        }

        public SendStateMachine(Context context) : base(context, SendState.Idle)
        {
            On(SendState.Idle, Action.Start, () =>
            {
                State = SendState.Ready;
            });

            On(SendState.Sending, Action.Sent, () =>
            {
                Context.PulseAll();
                State = SendState.Ready;
            });
        }

        public void Start(StreamSocket streamSocket)
        {
            m_streamSocket = streamSocket;

            Handle(Action.Start);
        }

        public bool TrySend(ref Frame frame)
        {
            if (State != SendState.Ready)
                return false;

            int payloadIndex;

            byte[] data;

            if (frame.Size <= 255)
            {
                data = new byte[2 + frame.Size];
                data[0] = 0; // More is not supported
                data[1] = (byte)frame.Size;

                payloadIndex = 2;
            }
            else
            {
                data = new byte[9 + frame.Size];
                data[0] = 2; // More is not supported

                NetworkOrderBitsConverter.PutInt64((long)frame.Size, data, 1);
                payloadIndex = 9;
            }

            frame.CopyTo(data, payloadIndex);
            frame.Close();
            frame.Init();

            State = SendState.Sending;

            SendData(data);

            return true;
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

        private void SendData(byte[] data)
        {
            var asyncAction = m_streamSocket.OutputStream.WriteAsync(data.AsBuffer());
            asyncAction.Completed = SendCompleted;
        }
    }
}