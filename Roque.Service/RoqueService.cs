using Cinchcast.Roque.Core;
using Cinchcast.Roque.Triggers;
using System.ServiceProcess;

namespace Cinchcast.Roque.Service
{
    public partial class RoqueService : ServiceBase
    {
        private WorkerHost host;
        private TriggerHost triggerHost;

        public RoqueService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            if (host == null)
            {
                host = new WorkerHost();
            }
            host.Start();
            if (triggerHost == null)
            {
                triggerHost = new TriggerHost();
            }
            triggerHost.Start();
        }

        protected override void OnStop()
        {
            host.Stop();
            triggerHost.Stop();
        }
    }
}
