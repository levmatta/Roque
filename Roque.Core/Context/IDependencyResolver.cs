using System;
using System.Collections.Generic;
using System.Linq;

namespace Cinchcast.Roque.Core.Context
{
    public interface IDependencyResolver
    {
        T GetService<T>() where T : class;

        Object GetService(Type type);

        IEnumerable<T> GetServices<T>() where T : class;

        IEnumerable<Object> GetServices(Type type);

        Object CreateEventInterfaceProxy(IInterceptor interceptor, Type type, Type baseProxyType);
    }
}
