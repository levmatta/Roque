using Cinchcast.Roque.Core.Context;

namespace Cinchcast.Roque.Core
{
    /// <summary>
    /// Generates dynamic proxies that intercept any method call and enqueues a job on a <see cref="Roque.Core.Queue"/>
    /// </summary>
    public static class RoqueProxyGenerator
    {
        /// <summary>
        /// Creates a new proxy for <typeparamref name="T"/> class, all methods will be intercepted and enqueued for async execution 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queueName">the queue to send all invocations</param>
        /// <param name="enqueueAsync">if true enqueueing will be done async</param>
        /// <returns>the transparent proxy which enqueues all invocations for async execution</returns>
        public static T Create<T>(string queueName, IDependencyResolver resolver, bool enqueueAsync = false)
            where T : class
        {
            return Create<T>(Queue.Get(queueName), resolver, enqueueAsync);
        }

        /// <summary>
        /// Creates a new proxy for <typeparamref name="T"/> class, all methods will be intercepted and enqueued for async execution 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queue">the queue to send all invocations</param>
        /// <param name="enqueueAsync">if true enqueueing will be done async</param>
        /// <returns>the transparent proxy which enqueues all invocations for async execution</returns>
        public static T Create<T>(Queue queue, IDependencyResolver resolver, bool enqueueAsync = false)
            where T : class
        {
            var interceptor = new Interceptor<T>(queue);
            var proxy = resolver.CreateEventInterfaceProxy(interceptor, typeof(T), typeof(Proxy));
            (proxy as Proxy).Queue = interceptor.Queue;
            return proxy as T;
        }

    }
}
