using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZMTP.NET
{
    public class Context
    {        
        private object m_sync;
        private Queue<Action> m_actionsQueue; 

        public Context()
        {
            m_actionsQueue = new Queue<Action>();

            m_sync = new object();
        }

        public void PulseAll()
        {
            Monitor.PulseAll(m_sync);
        }

        public void Enter()
        {
            Monitor.Enter(m_sync);            
        }

        public void Exit()
        {
            // Before we leave the context lets handle all events                        
            while (m_actionsQueue.Count > 0)
            {
                Action action = m_actionsQueue.Dequeue();
                action();
            }

            Monitor.Exit(m_sync);
        }

        public bool Wait(TimeSpan timeout)
        {
            return Monitor.Wait(m_sync, timeout);
        }

        public void Wait()
        {
            Monitor.Wait(m_sync);
        }

        public void EnqueueAction(Action action)
        {
            if (Monitor.IsEntered(m_sync))
            {
                // we are the holding thread, let's finish with current handling and will handle the event at the end
                m_actionsQueue.Enqueue(action);
            }
            else            
            {
                // Enter and exit the context to handle the events on the current thread
                Enter();
                action();
                Exit();
            }
        }
    }
}
