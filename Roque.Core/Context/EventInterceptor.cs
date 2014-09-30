using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cinchcast.Roque.Core.Context
{
    /// <summary>
    /// An interceptor that supports events
    /// </summary>
    public class EventInterceptor : IInterceptor
    {
        public IDictionary<string, IList<Delegate>> EventDelegates = new Dictionary<string, IList<Delegate>>();
        public IDictionary<string, int> EventDelegatesCounts = new Dictionary<string, int>();
        private readonly object syncRoot = new object();

        public void Intercept(IInvocation invocation)
        {
            var concreteMethod = invocation.GetConcreteMethod();
            if (concreteMethod.Name == "GetHandlersForEvent")
            {
                IList<Delegate> delegates;
                if (EventDelegates.TryGetValue(invocation.Message.Args[0] as string, out delegates))
                {
                    invocation.ReturnValue = delegates.ToArray();
                }
                else
                {
                    invocation.ReturnValue = new Delegate[0];
                }
                return;
            }
            if (concreteMethod.Name == "BeginTrackingSubscriptions")
            {
                EventDelegatesCounts = EventDelegates.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
                return;
            }
            if (concreteMethod.Name == "GetEventsWithNewSubscriptions")
            {
                invocation.ReturnValue = EventDelegates.Where(kv => !EventDelegatesCounts.ContainsKey(kv.Key) || kv.Value.Count > EventDelegatesCounts[kv.Key])
                    .Select(kv => kv.Key).ToArray();
                return;
            }

            if (concreteMethod.Name.StartsWith("add_"))
            {
                string eventName = concreteMethod.Name.Substring(4);
                //LVM review
                var evenInfo = invocation.InvocationTarget.GetType().GetEvent(eventName);
                if (evenInfo != null)
                {
                    IList<Delegate> delegates;
                    lock (syncRoot)
                    {
                        if (!EventDelegates.TryGetValue(eventName, out delegates))
                        {
                            delegates = new List<Delegate>();
                            EventDelegates[eventName] = delegates;
                        }
                    }
                    delegates.Add((Delegate)invocation.Message.Args[0]);
                    return;
                }
            }
            if (concreteMethod.Name.StartsWith("remove_"))
            {
                string eventName = concreteMethod.Name.Substring(7);
                var evenInfo = invocation.InvocationTarget.GetType().GetEvent(eventName);
                if (evenInfo != null)
                {
                    IList<Delegate> delegates;
                    lock (syncRoot)
                    {
                        if (!EventDelegates.TryGetValue(eventName, out delegates))
                        {
                            delegates = new List<Delegate>();
                            EventDelegates[eventName] = delegates;
                        }
                    }
                    delegates.Remove((Delegate)invocation.Message.Args[0]);
                    return;
                }
            }
        }
    }
}
