using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZMTP.NET
{
    public class ConditionalVariable
    {      
        public void PulseAll()
        {
            Monitor.PulseAll(this);   
        }

        public bool Wait(TimeSpan timeout)
        {
            return Monitor.Wait(this, timeout);
        }

        public void Wait()
        {
            Monitor.Wait(this);
        }
    }
}
