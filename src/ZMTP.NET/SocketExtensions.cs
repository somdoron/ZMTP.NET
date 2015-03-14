using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZMTP.NET
{
    public static class SocketExtensions
    {
        public static void Send(this Socket socket, string text)
        {
            Frame frame = new Frame();
            frame.Init(Encoding.UTF8.GetBytes(text));

            socket.TrySend(ref frame, Timeout.InfiniteTimeSpan);

            frame.Close();
        }

        public static string ReceiveString(this Socket socket)
        {
            Frame frame = new Frame();

            socket.TryReceive(ref frame, Timeout.InfiniteTimeSpan);

            var text =  Encoding.UTF8.GetString(frame.Data, 0, frame.Size);

            frame.Close();

            return text;
        }
    }
}
