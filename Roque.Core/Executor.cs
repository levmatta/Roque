using Cinchcast.Roque.Core.Configuration;
using Cinchcast.Roque.Core.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Cinchcast.Roque.Core
{

    /// <summary>
    /// Executes a <see cref="Job"/> by invoking a method in a service class.
    /// <remarks>
    ///   - If job target type is an interface castle windsor container is used to obtain a service.
    ///   - Job retries are supported by using the <see cref="RetryOnAttribute"/> or throwing a <see cref="ShouldRetryException"/>
    /// </remarks>
    /// </summary>
    public class Executor
    {
        private readonly object syncRoot = new object();

        private readonly IDictionary<string, Target> targets = new Dictionary<string, Target>();

        public IDependencyResolver ServiceContainer { get; protected set; }

        public Executor(IDependencyResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException("Resolver must be provided.");
            ServiceContainer = resolver;
        }

        public virtual void Execute(Job job)
        {
            try
            {
                RoqueTrace.Source.Trace(TraceEventType.Information, "Job Starting: {0}", job.Method);
                DoExecute(job);
                RoqueTrace.Source.Trace(TraceEventType.Information, "Job Completed: {0}", job.Method);
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error executing job: {0}", ex.Message, ex);
                throw;
            }
        }

        protected virtual void DoExecute(Job job)
        {
            InvokeTarget(job);
        }

        protected virtual void InvokeTarget(Job job)
        {
            Target target = GetTarget(job.Target);
            if (job.IsEvent)
            {
                target.InvokeEvent(job.Method, job.Arguments);
            }
            else
            {
                target.Invoke(job.Method, job.Arguments);
            }
        }

        protected virtual Target GetTarget(string targetTypeName, bool ifNotFoundUseEventProxy = false)
        {
            Target target;
            var fullName = targetTypeName.Split(new[] { ',', ' ' }).First();
            lock (syncRoot)
            {
                if (!targets.TryGetValue(fullName, out target))
                {
                    var type = Type.GetType(targetTypeName);
                    if (type == null)
                    {
                        throw new ShouldRetryException(TimeSpan.FromSeconds(10), 0, new Exception("Type not found: " + targetTypeName));
                    }
                    target = new Target(type, ServiceContainer, ifNotFoundUseEventProxy);
                    targets[fullName] = target;
                }
            }
            return target;
        }

        public virtual void RegisterSubscriber(object subscriber, string sourceQueue = null, string queue = null)
        {
            var suscribeMethods = subscriber.GetType().GetMethods().Where(m => m.Name.StartsWith("Subscribe")).ToArray();
            foreach (var suscribeMethod in suscribeMethods)
            {
                List<object> parameters = new List<object>();

                foreach (var paramInfo in suscribeMethod.GetParameters())
                {
                    try
                    {
                        var instance = GetTarget(paramInfo.ParameterType.AssemblyQualifiedName, true).Instance;
                        parameters.Add(instance);
                        if (instance is IEventProxy)
                        {
                            ((IEventProxy)instance).BeginTrackingSubscriptions();
                        }
                    }
                    catch (Exception ex)
                    {
                        RoqueTrace.Source.Trace(TraceEventType.Error, "Error injecting subscriber parameter: {0}. Method: {1}, Parameter: {2}, Expected Type: {3}",
                            ex.Message, suscribeMethod.Name, paramInfo.Name, paramInfo.ParameterType.FullName, ex);
                        RoqueTrace.Source.Trace(TraceEventType.Error, "Error details: {0}", ex.ToString());
                        throw;
                    }
                }
                suscribeMethod.Invoke(subscriber, parameters.ToArray());
                if (!string.IsNullOrWhiteSpace(sourceQueue) && !string.IsNullOrWhiteSpace(queue))
                {
                    foreach (var paramInfo in suscribeMethod.GetParameters())
                    {
                        var instance = GetTarget(paramInfo.ParameterType.AssemblyQualifiedName, true).Instance;
                        if (instance is IEventProxy)
                        {
                            string[] eventNames = ((IEventProxy)instance).GetEventsWithNewSubscriptions();
                            foreach (string eventName in eventNames)
                            {
                                Queue.Get(queue).ReportEventSubscription(sourceQueue, paramInfo.ParameterType.FullName, eventName);
                            }
                            RoqueTrace.Source.Trace(TraceEventType.Verbose, "Reported event subscriptions. Events: {0}:{1}, Source Queue: {2}, Queue: {3}",
                                paramInfo.ParameterType.FullName, string.Join(",", eventNames), sourceQueue, queue);
                        }
                    }
                }
            }
        }

        public virtual void RegisterSubscribersForWorker(Worker worker)
        {
            if (string.IsNullOrEmpty(worker.Name))
            {
                return;
            }

            var workerConfig = Configuration.Roque.Settings.Workers[worker.Name];
            if (workerConfig == null || workerConfig.Subscribers.Count < 1)
            {
                return;
            }

            foreach (var subscriberConfig in workerConfig.Subscribers.OfType<SubscriberElement>())
            {
                try
                {
                    string sourceQueue = subscriberConfig.SourceQueue;
                    if (string.IsNullOrEmpty(sourceQueue))
                    {
                        sourceQueue = Queue.DefaultEventQueueName;
                    }
                    RegisterSubscriber(worker.Resolver.GetService(Type.GetType(subscriberConfig.SubscriberType)), sourceQueue, worker.Queue.Name);
                }
                catch (Exception ex)
                {
                    RoqueTrace.Source.Trace(TraceEventType.Error, "Error registering subscriber: {0}. Type: {1}",
                        ex.Message, subscriberConfig.SubscriberType, ex);
                    RoqueTrace.Source.Trace(TraceEventType.Error, "Error details: {0}", ex.ToString());
                    throw;
                }
            }
        }

    }
}
