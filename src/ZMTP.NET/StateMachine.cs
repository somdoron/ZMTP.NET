using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZMTP.NET
{
    class StateMachine<TState, TAction>
    {
        private Dictionary<Tuple<TState, TAction>, Action> m_handlers;
        private Dictionary<Tuple<TState, TAction>, Action<Object>> m_handlersWithParameter;

        public StateMachine(Context context, TState initial)
        {
            Context = context;
            State = initial;
            m_handlers = new Dictionary<Tuple<TState, TAction>, Action>();
            m_handlersWithParameter = new Dictionary<Tuple<TState, TAction>, Action<object>>();
        }

        public TState State { get; protected set; }

        protected Context Context { get; private set; }

        protected void On(TState state, TAction action, Action handler)
        {
            m_handlers.Add(new Tuple<TState, TAction>(state, action), handler);
        }

        protected void On<T>(TState state, TAction action, Action<T> handler)
        {
            // TODO: throw exception if the parameter is of the wrong type
            m_handlersWithParameter.Add(new Tuple<TState, TAction>(state, action), o => handler((T)o));
        }

        protected void FireEvent(EventHandler handler)
        {
            if (handler != null)
            {
                Context.EnqueueAction(() => handler(this, EventArgs.Empty));
            }
        }

        public void Handle(TAction action)
        {
            Context.EnqueueAction(() =>
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
            });
        }

        public void Handle<T>(TAction action, T parameter)
        {
            Context.EnqueueAction(() =>
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
            });
        }
    }
}
