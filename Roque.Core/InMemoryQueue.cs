using System.Collections.Concurrent;

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// in-memory queue implementation
    /// </summary>
    public class InMemoryQueue : Queue
    {
        protected BlockingCollection<string> queue = new BlockingCollection<string>();

        public InMemoryQueue(string name, IDictionary<string, string> setings)
            : base(name, setings)
        {
        }

        protected override void EnqueueJson(string data)
        {
            queue.Add(data);
        }

        protected override string DequeueJson(Worker worker, int timeoutSeconds)
        {
            string data;
            if (queue.TryTake(out data, timeoutSeconds * 1000))
            {
                return data;
            }
            return null;
        }


        protected override string PeekJson(out long length)
        {
            length = queue.Count;
            if (length < 1)
            {
                return null;
            }
            return queue.FirstOrDefault();
        }


        protected override void EnqueueJsonEvent(string data, string target, string eventName)
        {
            throw new NotImplementedException();
        }

        protected override void DoReportEventSubscription(string sourceQueue, string target, string eventName)
        {
            throw new NotImplementedException();
        }

        protected override DateTime? DoGetTimeOfLastJobCompleted()
        {
            return null;
        }

        public override IDictionary<string, string[]> GetSubscribers()
        {
            return null;
        }

        public override string[] GetSubscribersForEvent(string target, string eventName)
        {
            return null;
        }
    }
}
