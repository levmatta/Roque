namespace Cinchcast.Roque.Core
{
    using System;
    using System.Linq;

    /// <summary>
    /// A Queue that keeps tracking of in progress jobs for each worker. Provides support for fail recovery.
    /// </summary>
    public interface IQueueWithInProgressData
    {
        string GetInProgressJson(Worker worker);

        void JobCompleted(Worker worker, Job job, bool failed);
    }
}
