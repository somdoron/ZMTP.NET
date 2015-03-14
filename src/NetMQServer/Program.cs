using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQServer
{
    class Program
    {
        static void Main(string[] args)
        {
            using (NetMQContext context = NetMQContext.Create())
            using (RouterSocket router = context.CreateRouterSocket())
            {
                router.Bind("tcp://*:33333");

                while (true)
                {
                    byte[] identity = router.Receive();
                    string message = router.ReceiveString();

                    Console.WriteLine(message);

                    router.SendMore(identity).Send(message);
                }
            }
        }
    }
}
