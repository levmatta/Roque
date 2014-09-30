using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;

namespace Cinchcast.Roque.Core.Context
{
    public interface IInvocation
    {
        Object InvocationTarget { get; }

        Object ReturnValue { get; set; }

        IMethodCallMessage Message { get; set; }

        void Proceed();

        MethodBase GetConcreteMethod();
    }
}
