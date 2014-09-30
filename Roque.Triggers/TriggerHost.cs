using System;
using System.Linq;
using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Triggers
{
    /// <summary>
    /// Hosts a TriggerWatcher into a separate AppDomain
    /// </summary>
    public class TriggerHost : AppDomainHost<TriggerHost.TriggerProcess>
    {
        public class TriggerProcess : AppDomainHost.Process
        {
            public override void OnStart(dynamic parameters)
            {
                Trigger.All.Start();
            }

            public override void OnStop()
            {
                Trigger.All.Stop().Wait();
            }
        }
    }
}
