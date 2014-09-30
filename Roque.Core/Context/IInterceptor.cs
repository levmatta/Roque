using System;
using System.Linq;

namespace Cinchcast.Roque.Core.Context
{
    public interface IInterceptor
    {
        void Intercept(IInvocation invocation);
    }
}
