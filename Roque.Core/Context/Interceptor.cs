using System;
using System.Linq;

namespace Cinchcast.Roque.Core.Context
{
    public class Interceptor<T> : IInterceptor
    {
        public Queue Queue { get; private set; }

        protected readonly string typeName;

        protected readonly bool enqueueAsync;

        public Interceptor(Queue queue, bool enqAsync = false)
        {
            Queue = queue;
            typeName = typeof(T).AssemblyQualifiedName;
            enqueueAsync = enqAsync;
        }

        public virtual void Intercept(IInvocation invocation)
        {
            var job = Job.Create(typeName, invocation.Message.MethodName, invocation.Message.Args);
            if (enqueueAsync)
            {
                Queue.EnqueueAsync(job);
            }
            else
            {
                Queue.Enqueue(job);
            }
        }
    }
}
