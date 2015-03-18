using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;

namespace ZMTP.NET
{
    public class Endpoint
    {
        private readonly Context m_context;
        private readonly SocketType m_socketType;

        private string m_hostName;
        private int m_port;

        private HandshakeStateMachine m_handshake;
        private ReceiveStateMachine m_receiveStateMachine;
        private SendStateMachine m_sendStateMachine;
        private StreamSocket m_streamSocket;        

        public Endpoint(Context context, SocketType socketType, string address)
        {            
            m_context = context;
            m_socketType = socketType;

            Uri uri = new Uri(address);

            if (uri.Scheme != "tcp" || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.Query))
                throw new ArgumentException("invalid address");

            m_hostName = uri.Host;
            m_port = uri.Port;

            m_handshake = new HandshakeStateMachine(m_context, m_hostName, m_port, m_socketType);
            m_receiveStateMachine = new ReceiveStateMachine(m_context);
            m_sendStateMachine = new SendStateMachine(m_context);
        }

        public void Start()
        {
            m_streamSocket = new StreamSocket();

            m_handshake.Completed += HandshakeCompleted;
            
            m_handshake.Start(m_streamSocket);
        }

        private void HandshakeCompleted(object sender, EventArgs e)
        {
            m_receiveStateMachine.Start(m_streamSocket);
            m_sendStateMachine.Start(m_streamSocket);

            m_context.PulseAll();
        }

        public bool TrySend(ref Frame frame)
        {         
            if (m_handshake.State != HandshakeStateMachine.HandshakeState.Ready)
                return false;

            return m_sendStateMachine.TrySend(ref frame);                    
        }

        public bool TryReceive(ref Frame frame)
        {            
            if (m_handshake.State != HandshakeStateMachine.HandshakeState.Ready)
                return false;

            return m_receiveStateMachine.TryReceive(ref frame);
        }     
    }
}
