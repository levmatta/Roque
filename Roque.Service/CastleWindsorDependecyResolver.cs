using Castle.Windsor;
using Cinchcast.Roque.Core.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cinchcast.Roque.Service
{
    class CastleWindsorDependecyResolver : AbstractDependecyResolver
    {
        protected readonly IWindsorContainer container;

        public CastleWindsorDependecyResolver(IWindsorContainer container)
        {
            this.container = container;
        }
        protected override IEnumerable<Object> DoGetServices(Type type)
        {
            return container.ResolveAll(type) as IEnumerable<Object>;
        }

        public override object CreateEventInterfaceProxy(IInterceptor interceptor, Type type, Type baseProxyType)
        {
            throw new NotImplementedException();
        }
    }
}
