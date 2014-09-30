namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// An array of <see cref="Worker"/>
    /// </summary>
    public class WorkerArray : IEnumerable<Worker>
    {
        private readonly Worker[] workers;

        public WorkerArray(params Worker[] workers)
        {
            this.workers = workers;
        }

        /// <summary>
        /// Starts all workers
        /// </summary>
        public void Start(bool onlyAutoStart = false)
        {
            ForEach(worker =>
            {
                if (!onlyAutoStart || worker.AutoStart)
                {
                    worker.Start();
                }
            });
        }

        /// <summary>
        /// Request stop of all workers
        /// </summary>
        /// <returns></returns>
        public Task Stop()
        {
            var tasks = this.Select(worker => worker.Stop()).ToArray();
            return Task.Factory.StartNew(() =>
            {
                Task.WaitAll(tasks);
            });
        }

        /// <summary>
        /// Request stop of all workers, and blocks until they are all stopped.
        /// </summary>
        /// <returns></returns>
        public void StopAndWait()
        {
            Stop().Wait();
        }

        public void ForEach(Action<Worker> action)
        {
            foreach (var worker in workers)
            {
                action(worker);
            }
        }

        public IEnumerator<Worker> GetEnumerator()
        {
            return workers.AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return workers.GetEnumerator();
        }
    }
}
