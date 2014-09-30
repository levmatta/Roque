namespace Cinchcast.Roque.Common
{
    using System;
    using System.Linq;

    /// <summary>
    /// Work service interface example
    /// </summary>
    public interface IRoqueCommander
    {
        void StopWorker(string name);
        void StartWorker(string name);
    }
}
