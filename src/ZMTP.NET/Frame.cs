using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;

namespace ZMTP.NET
{
    public struct Frame
    {                
        public int Size { get; private set; }

        public byte[] Data { get; private set; }        

        internal void CopyTo(byte[] data, int payloadIndex)
        {
            System.Buffer.BlockCopy(Data, 0, data, payloadIndex, Size);
        }

        public void Init()
        {
            
        }

        public void Init(byte[] data)
        {
            Data = data;
            Size = data.Length;
        }

        public void Close()
        {
            
        }
    }
}
