using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Common
{
    using Cinchcast.Roque.Core.Context;

    /// <summary>
    /// Work service implementation example
    /// </summary>
    public class RoqueCommander : IRoqueCommander
    {
        protected readonly IDependencyResolver resolver;

        public RoqueCommander(IDependencyResolver resolver)
        {
            this.resolver = resolver;
        }

        public void StopWorker(string name)
        {
            Worker.Get(name, resolver).Stop().Wait();
        }

        public void StartWorker(string name)
        {
            var worker = Worker.Get(name, resolver);
            if (worker.State == Worker.WorkerState.Created || worker.State == Worker.WorkerState.Stopped)
            {
                worker.Start();
            }
        }
    }
}
