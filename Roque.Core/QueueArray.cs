namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// An array of <see cref="Queue"/>
    /// </summary>
    public class QueueArray : IEnumerable<Queue>
    {

        private readonly Queue[] queues;

        public QueueArray(params Queue[] workers)
        {
            queues = workers;
        }

        public void ForEach(Action<Queue> action)
        {
            foreach (var worker in queues)
            {
                action(worker);
            }
        }

        public IEnumerator<Queue> GetEnumerator()
        {
            return queues.AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return queues.GetEnumerator();
        }
    }
}
