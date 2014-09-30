using System;
using System.Linq;

namespace Cinchcast.Roque.Core.Context
{
    /// <summary>
    /// An interceptor that performs no action (doesn't call Proceed)
    /// </summary>
    public class NoActionInterceptor : IInterceptor
    {
        private static NoActionInterceptor defaultInterceptor;

        public static NoActionInterceptor Default
        {
            get
            {
                if (defaultInterceptor == null)
                {
                    defaultInterceptor = new NoActionInterceptor();
                }
                return defaultInterceptor;
            }
        }

        private NoActionInterceptor()
        {
        }

        public void Intercept(IInvocation invocation)
        {
            // do nothing
        }
    }
}
