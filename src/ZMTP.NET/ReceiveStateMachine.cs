using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;

namespace ZMTP.NET
{
    class ReceiveStateMachine : StateMachine<ReceiveStateMachine.ReceiveState, ReceiveStateMachine.ReceiveAction>
    {
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

        private int m_size;
        private byte[] m_data;
        private StreamSocket m_streamSocket;

        public ReceiveStateMachine(Context context) : base(context, ReceiveState.Idle)
        {            
            On(ReceiveState.Idle, ReceiveAction.Start, () =>
            {                
                State = ReceiveState.Flag;
                ReceiveData(1);
            });

            On(ReceiveState.MessageReady, ReceiveAction.Fetched, () =>
            {
                State = ReceiveState.Flag;
                ReceiveData(1);
            });

            On<IBuffer>(ReceiveState.Flag, ReceiveAction.Received, buffer =>
            {
                byte[] data = buffer.ToArray();
                int flag = data[0];

                // TODO: more bit is not supported at the moment
                // m_receivingMessage.More = (flag & 1) != 0;
                Debug.Assert((flag & 1) == 0);

                bool eightSize = (flag & 2) != 0;

                if (eightSize)
                {
                    State = ReceiveState.EightByte;
                    ReceiveData(8);
                }
                else
                {
                    State = ReceiveState.OneByte;
                    ReceiveData(1);
                }
            });

            On<IBuffer>(ReceiveState.OneByte, ReceiveAction.Received, buffer =>
            {
                byte[] data = buffer.ToArray();
                m_size = data[0];

                if (m_size > 0)
                {
                    State = ReceiveState.Message;
                    ReceiveData(m_size);
                }
                else
                {
                    m_data = new byte[0];

                    State = ReceiveState.MessageReady;

                    Context.PulseAll();
                }
            });

            On<IBuffer>(ReceiveState.EightByte, ReceiveAction.Received, buffer =>
            {
                byte[] data = buffer.ToArray();
                m_size = (int)NetworkOrderBitsConverter.ToInt64(data);

                State = ReceiveState.Message;
                ReceiveData(m_size);
            });

            On<IBuffer>(ReceiveState.Message, ReceiveAction.Received, buffer =>
            {
                m_data = buffer.ToArray();                

                State = ReceiveState.MessageReady;

                // let threads now that state has changed
                Context.PulseAll();
            });
        }

        public void Start(StreamSocket streamSocket)
        {
            m_streamSocket = streamSocket;
            Handle(ReceiveAction.Start);
        }

        public bool TryReceive(ref Frame frame)
        {
            if (State != ReceiveState.MessageReady)
                return false;

            frame.Close();
            frame.Init(m_data);
                        
            Handle(ReceiveAction.Fetched);

            return true;
        }       

        private void ReceiveData(int size)
        {
            var asyncAction = m_streamSocket.InputStream.ReadAsync(new Buffer((uint)size), (uint)size, InputStreamOptions.ReadAhead);
            asyncAction.Completed = ReceiveMessageCompleted;
        }

        private void ReceiveMessageCompleted(IAsyncOperationWithProgress<IBuffer, uint> info, AsyncStatus status)
        {
            if (status == AsyncStatus.Completed)
            {
                Handle(ReceiveAction.Received, info.GetResults());
            }
            else
            {
                Handle(ReceiveAction.ReceiveFailed);
            }
        }
    }
}
