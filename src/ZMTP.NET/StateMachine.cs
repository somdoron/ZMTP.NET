using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMTP.NET
{
    class StateMachine<TState, TAction>
    {
        private readonly ConditionalVariable m_conditionalVariable;

        private Dictionary<Tuple<TState, TAction>, Action> m_handlers;
        private Dictionary<Tuple<TState, TAction>, Action<Object>> m_handlersWithParameter;        

        public StateMachine(ConditionalVariable conditionalVariable, TState initial)
        {
            m_conditionalVariable = conditionalVariable;
            State = initial;
            m_handlers = new Dictionary<Tuple<TState, TAction>, Action>();
            m_handlersWithParameter = new Dictionary<Tuple<TState, TAction>, Action<object>>();            
        }

        public TState State { get; set; }

        public void On(TState state, TAction action, Action handler)
        {
            m_handlers.Add(new Tuple<TState, TAction>(state, action),handler);
        }

        public void On<T>(TState state, TAction action, Action<T> handler)
        {
            // TODO: throw exception if the parameter is of the wrong type
            m_handlersWithParameter.Add(new Tuple<TState, TAction>(state, action), o => handler((T) o));
        }

        public void Feed(TAction action)
        {
            lock (m_conditionalVariable)
            {
                Action handler;

                if (m_handlers.TryGetValue(new Tuple<TState, TAction>(State, action), out handler))
                {
                    handler();
                }
                else
                {
                    throw new ArgumentOutOfRangeException("action", "no handler for action");
                }
            }
        }

        public void Feed<T>(TAction action, T parameter)
        {
            lock (m_conditionalVariable)
            {
                Action<object> handler;

                if (m_handlersWithParameter.TryGetValue(new Tuple<TState, TAction>(State, action), out handler))
                {
                    handler(parameter);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("action", "no handler for action");
                }
            }
        }
    }
}
