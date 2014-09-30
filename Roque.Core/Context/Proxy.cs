using System;
using System.Linq;

namespace Cinchcast.Roque.Core.Context
{
    /// <summary>
    /// Base class for dynamic work proxies. When a method on an instance is invoked a new job is sent to the <see cref="Queue"/>
    /// </summary>
    public class Proxy
    {
        protected Queue queue;
        public Queue Queue
        {
            get
            {
                return queue;
            }
            internal set
            {
                if (queue != null)
                {
                    throw new InvalidOperationException("Proxy Queue cannot be modified");
                }
                queue = value;
            }
        }
    }
}
