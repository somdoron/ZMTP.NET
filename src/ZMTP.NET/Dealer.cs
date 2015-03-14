using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMTP.NET
{
    public class Dealer : Socket
    {
        private Endpoint m_endpoint;

        public Dealer() : base(SocketType.Dealer)
        {
        }

        protected override void Attach(Endpoint endpoint)
        {
            // currently only when endpoint is supported
            if (m_endpoint != null)
                throw new InvalidOperationException("Only one endpoint is currently supported");

            m_endpoint = endpoint;
        }

        protected override void Detach(Endpoint endpoint)
        {
            m_endpoint = null;
        }

        protected override bool TrySendInternal(ref Frame frame)
        {
            if (m_endpoint == null)
                return false;

            return m_endpoint.TrySend(ref frame);
        }

        protected override bool TryReceiveInternal(ref Frame frame)
        {
            if (m_endpoint == null)
                return false;

            return m_endpoint.TryReceive(ref frame);
        }
    }
}
