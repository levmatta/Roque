using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cinchcast.Roque.Core.Context
{
    public class Target
    {
        public object Instance { get; private set; }

        public Type InstanceType { get; private set; }

        public IDependencyResolver ServiceContainer { get; private set; }

        public IDictionary<string, MethodInfo> Methods { get; private set; }

        public IDictionary<string, EventInfo> Events { get; private set; }

        public Target(Type type, IDependencyResolver resolver, bool ifNotFoundUseEventProxy = false)
        {
            InstanceType = type;
            ServiceContainer = resolver;
            Methods = new Dictionary<string, MethodInfo>();
            Events = new Dictionary<string, EventInfo>();

            if (type.IsInterface || (type.IsAbstract && !type.IsSealed))
            {
                // get an implementation for this interface or abstract class
                Instance = ServiceContainer.GetService(type);
            }
            else
            {
                try
                {
                    Instance = (type.IsAbstract) ? /* static class */ null : Instance = resolver.GetService(type);
                }
                catch
                {
                    if (ifNotFoundUseEventProxy && type.IsInterface)
                    {
                        Instance = resolver.CreateEventInterfaceProxy(null, type, typeof(Proxy)); //LVM
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public void Invoke(string methodName, params string[] parameters)
        {
            MethodInfo method = GetMethod(methodName);
            var methodParametersInfo = method.GetParameters();
            if (methodParametersInfo.Length != parameters.Length) throw new ArgumentException(
                    String.Format("Wrong number of parameters. Method: {0}, Expected: {1}, Received: {2}", methodName, methodParametersInfo.Length, parameters.Length)
                    );
            var parameterValues = PrepareParameterValues(parameters, method, methodParametersInfo);
            try
            {
                method.Invoke(Instance, parameterValues.ToArray());
            }
            catch (TargetInvocationException ex)
            {
                var jobException = ex.InnerException;
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error invoking job target: {0}\n\n{1}", jobException.Message, jobException);

                var jobExceptionType = jobException.GetType();
                if (jobException is ShouldRetryException) throw jobException;
                var invokedMethod = (Instance == null ? method : Instance.GetType().GetMethod(methodName));
                if (!Retry(invokedMethod, jobException, jobExceptionType)) throw;
            }
        }

        public void InvokeEvent(string eventName, params string[] parameters)
        {
            EventInfo eventInfo = GetEvent(eventName);
            Type eventArgsType = GetEventArgsType(eventInfo);
            EventArgs eventArgsValue = PrepareEventParameterValues(parameters, eventInfo, eventArgsType);

            MethodInfo handlerMethod = null;
            try
            {
                if (Instance is IEventProxy)
                {
                    var handlers = ((IEventProxy)Instance).GetHandlersForEvent(eventInfo.Name);
                    if (handlers.Length > 0)
                    {
                        foreach (var handler in handlers)
                        {
                            handlerMethod = handler.Method;
                            handlerMethod.Invoke(handler.Target, new object[] { Instance, eventArgsValue });
                        }
                    }
                    else
                    {
                        RoqueTrace.Source.Trace(TraceEventType.Warning, "No suscribers found for event: {0}", eventInfo.Name);
                    }
                }
                else
                {
                    var privateDelegatesField = Instance.GetType().GetField(eventInfo.Name, BindingFlags.Instance | BindingFlags.NonPublic);
                    var eventDelegate = (MulticastDelegate)privateDelegatesField.GetValue(Instance);
                    if (eventDelegate != null)
                    {
                        foreach (var handler in eventDelegate.GetInvocationList())
                        {
                            handler.Method.Invoke(handler.Target, new object[] { Instance, eventArgsValue });
                        }
                    }
                }
            }
            catch (TargetInvocationException ex)
            {
                var jobException = ex.InnerException;
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error invoking event handler: {0}\n\n{1}", jobException.Message, jobException);
                var jobExceptionType = jobException.GetType();
                if (jobException is ShouldRetryException)
                {
                    throw jobException;
                }
                if (handlerMethod != null && !Retry(handlerMethod, jobException, jobExceptionType)) throw;
            }
        }

        protected virtual MethodInfo GetMethod(string methodName)
        {
            MethodInfo method;
            if (!Methods.TryGetValue(methodName, out method))
            {
                try
                {
                    method = InstanceType.GetMethod(methodName);
                }
                catch (AmbiguousMatchException ex)
                {
                    throw new Exception("Invoked methods can't have multiple overloads: " + methodName, ex);
                }
                Methods[methodName] = method;
            }
            return method;
        }

        protected virtual EventInfo GetEvent(string eventName)
        {
            EventInfo eventInfo;
            if (!Events.TryGetValue(eventName, out eventInfo))
            {
                eventInfo = InstanceType.GetEvent(eventName);
                if (eventInfo == null && InstanceType.IsInterface)
                {
                    // search event in parent interfaces
                    foreach (var parentInterface in InstanceType.GetInterfaces())
                    {
                        eventInfo = parentInterface.GetEvent(eventName);
                        if (eventInfo != null)
                        {
                            break;
                        }
                    }
                }
                if (eventInfo == null)
                {
                    throw new Exception(string.Format("Event not found. Type: {0}, EventName: {1}", InstanceType.FullName, eventName));
                }
                Events[eventName] = eventInfo;
            }
            return eventInfo;
        }

        protected virtual List<object> PrepareParameterValues(string[] parameters, MethodInfo method, ParameterInfo[] methodParametersInfo)
        {
            var parameterValues = new List<object>();
            for (int index = 0; index < parameters.Length; index++)
            {
                try
                {
                    parameterValues.Add(JsonConvert.DeserializeObject(parameters[index], methodParametersInfo[index].ParameterType));
                }
                catch (Exception ex)
                {
                    RoqueTrace.Source.Trace(TraceEventType.Error, "Error deserializing parameter: {0}. Method: {1}, Parameter: {2}, Expected Type: {3}",
                        ex.Message, method.Name, methodParametersInfo[index].Name, methodParametersInfo[index].ParameterType.FullName, ex);
                    throw;
                }
            }
            return parameterValues;
        }

        protected virtual EventArgs PrepareEventParameterValues(string[] parameters, EventInfo eventInfo, Type eventArgsType)
        {
            EventArgs eventArgsValue;
            try
            {
                if (parameters.Length > 0)
                {
                    eventArgsValue = JsonConvert.DeserializeObject(parameters[0], eventArgsType) as EventArgs;
                }
                else
                {
                    eventArgsValue = EventArgs.Empty;
                }
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error deserializing event args: {0}. Event: {1}, Expected Type: {2}",
                     ex.Message, eventInfo.Name, eventArgsType.FullName, ex);
                throw;
            }
            return eventArgsValue;
        }

        protected virtual bool Retry(MethodInfo method, Exception jobException, Type jobExceptionType)
        {
            // IS the attribute on the method ?
            var retryOn = method.GetCustomAttributes(typeof(RetryOnAttribute), true)
                .OfType<RetryOnAttribute>()
                .FirstOrDefault(attr => attr.ExceptionType.IsAssignableFrom(jobExceptionType));
            if (retryOn == null) // IS the attribute on the class ?
            {
                retryOn = method.DeclaringType.GetCustomAttributes(typeof(RetryOnAttribute), true)
                    .OfType<RetryOnAttribute>()
                    .FirstOrDefault(attr => attr.ExceptionType.IsAssignableFrom(jobExceptionType));
            }
            // If Found call retry logic (using the exception)
            if (retryOn != null && !(retryOn is DontRetryOnAttribute))
            {
                throw retryOn.CreateException(jobException);
            }
            return false;
        }

        public static Type GetEventArgsType(EventInfo eventType)
        {
            Type t = eventType.EventHandlerType;
            MethodInfo m = t.GetMethod("Invoke");

            var parameters = m.GetParameters();
            return parameters[1].ParameterType;
        }
    }
}
