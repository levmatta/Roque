using System;
using System.Collections.Generic;
using System.Linq;

namespace Cinchcast.Roque.Core.Context
{
    public abstract class AbstractDependecyResolver : IDependencyResolver
    {
        public T GetService<T>() where T : class
        {
            var res = DoGetServices(typeof(T));
            return res.FirstOrDefault() as T;
        }

        public object GetService(Type type)
        {
            var res = DoGetServices(type);
            return res.FirstOrDefault();
        }

        public IEnumerable<T> GetServices<T>() where T : class
        {
            var res = DoGetServices(typeof(T));
            return res as IEnumerable<T>;
        }

        public IEnumerable<object> GetServices(Type type)
        {
            var res = DoGetServices(type);
            return res;
        }

        protected abstract IEnumerable<Object> DoGetServices(Type type);

        public abstract object CreateEventInterfaceProxy(IInterceptor interceptor, Type type, Type baseProxyType);
    }
}
