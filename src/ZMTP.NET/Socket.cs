using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace ZMTP.NET
{
    public enum SocketType
    {
        Dealer = 5,
    }

    public abstract class Socket : IDisposable
    {                
        private ConditionalVariable m_conditionalVariable;        

        public Socket(SocketType socketType)
        {
            SocketType = socketType;            
            m_conditionalVariable = new ConditionalVariable();
        }

        public SocketType SocketType { get; private set; }

        public void Connect(string address)
        {
            lock (m_conditionalVariable)
            {
                Endpoint endpoint = new Endpoint(m_conditionalVariable, SocketType, address);
                endpoint.Start();

                Attach(endpoint);
            }
        }

        public bool TrySend(ref Frame frame, TimeSpan timeout)
        {
            lock (m_conditionalVariable)
            {                
                bool isMessageSent = TrySendInternal(ref frame);

                if (isMessageSent)
                    return true;

                Stopwatch stopwatch = Stopwatch.StartNew();

                bool infinite = timeout == Timeout.InfiniteTimeSpan;

                while (infinite || stopwatch.Elapsed < timeout)
                {
                    TimeSpan actualTimeout = timeout - stopwatch.Elapsed;

                    if (actualTimeout.Ticks < 0)
                        actualTimeout = TimeSpan.Zero;

                    bool signalled;

                    if (infinite)
                    {
                        m_conditionalVariable.Wait();
                        signalled = true;
                    }
                    else
                        signalled = m_conditionalVariable.Wait(actualTimeout);

                    if (signalled)
                    {
                        isMessageSent = TrySendInternal(ref frame);

                        if (isMessageSent)
                            return true;
                    }                    
                }

                return false;
            }
        }

        public bool TryReceive(ref Frame frame, TimeSpan timeout)
        {
            lock (m_conditionalVariable)
            {                
                bool isMessageReceived = TryReceiveInternal(ref frame);

                if (isMessageReceived)
                    return true;

                Stopwatch stopwatch = Stopwatch.StartNew();

                bool infinite = timeout == Timeout.InfiniteTimeSpan;

                while (infinite || stopwatch.Elapsed < timeout)
                {
                    TimeSpan actualTimeout = timeout - stopwatch.Elapsed;

                    if (actualTimeout.Ticks < 0)
                        actualTimeout = TimeSpan.Zero;

                    bool signalled;

                    if (infinite)
                    {
                        m_conditionalVariable.Wait();
                        signalled = true;
                    }
                    else
                        signalled = m_conditionalVariable.Wait(actualTimeout);

                    if (signalled)
                    {
                        isMessageReceived = TryReceiveInternal(ref frame);

                        if (isMessageReceived)
                            return true;
                    }
                }

                return false;
            }
        }

        protected abstract void Attach(Endpoint endpoint);

        protected abstract void Detach(Endpoint endpoint);

        protected abstract bool TrySendInternal(ref Frame frame);

        protected abstract bool TryReceiveInternal(ref Frame frame);

        public void Dispose()
        {
            
        }
    }
}
