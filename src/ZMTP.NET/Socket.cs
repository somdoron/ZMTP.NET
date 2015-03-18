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
        private Context m_context;

        public Socket(SocketType socketType)
        {
            SocketType = socketType;
            m_context = new Context();
        }

        public SocketType SocketType { get; private set; }

        public void Connect(string address)
        {
            m_context.Enter();

            try
            {
                Endpoint endpoint = new Endpoint(m_context, SocketType, address);
                endpoint.Start();

                Attach(endpoint);
            }
            finally
            {
                m_context.Exit();
            }
        }

        public bool TrySend(ref Frame frame, TimeSpan timeout)
        {
            m_context.Enter();

            try
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
                        m_context.Wait();
                        signalled = true;
                    }
                    else
                        signalled = m_context.Wait(actualTimeout);

                    if (signalled)
                    {
                        isMessageSent = TrySendInternal(ref frame);

                        if (isMessageSent)
                            return true;
                    }
                }

                return false;
            }
            finally
            {
                m_context.Exit();
            }
        }

        public bool TryReceive(ref Frame frame, TimeSpan timeout)
        {
            m_context.Enter();

            try
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
                        m_context.Wait();
                        signalled = true;
                    }
                    else
                        signalled = m_context.Wait(actualTimeout);

                    if (signalled)
                    {
                        isMessageReceived = TryReceiveInternal(ref frame);

                        if (isMessageReceived)
                            return true;
                    }
                }

                return false;
            }
            finally
            {
                m_context.Exit();
            }
        }

        protected abstract void Attach(Endpoint endpoint);

        protected abstract void Detach(Endpoint endpoint);

        protected abstract bool TrySendInternal(ref Frame frame);

        protected abstract bool TryReceiveInternal(ref Frame frame);

        public void Dispose()
        {
            m_context.Enter();

            try
            {
            }
            finally
            {
                m_context.Exit();
            }
        }
    }
}
