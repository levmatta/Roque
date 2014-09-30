using Cinchcast.Roque.Core.Context;

namespace Cinchcast.Roque.Core
{
    /// <summary>
    /// Runs workers on a separate AppDomain.
    /// </summary>
    public class WorkerHost : AppDomainHost<WorkerHost.WorkerProcess>
    {
        public static IDependencyResolver Resolver {get; set;}

        public class WorkerProcess : AppDomainHost.Process
        {
            private WorkerArray workerArray;

            public override void OnStart(dynamic parameters)
            {
                string worker = parameters as string;
                workerArray = string.IsNullOrEmpty(worker) ? Worker.GetAll(Resolver) : new WorkerArray(Worker.Get(worker, Resolver));
                workerArray.Start(onlyAutoStart: string.IsNullOrEmpty(worker));
            }

            public override void OnStop()
            {
                workerArray.StopAndWait();
            }
        }
    }
}
