using System;
using System.Linq;

namespace Cinchcast.Roque.Core.Context
{
    public interface IEventProxy
    {
        Delegate[] GetHandlersForEvent(string eventName);
        void BeginTrackingSubscriptions();
        string[] GetEventsWithNewSubscriptions();
    }
}
