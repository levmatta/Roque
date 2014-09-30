using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Core.Configuration;
using Cinchcast.Roque.Redis;
using StackExchange.Redis;

namespace Cinchcast.Roque.Triggers
{
    /// <summary>
    /// Base class for triggers, that when executed add a job to a Queue.
    /// </summary>
    public class Trigger
    {
        public Trigger()
        {
            Settings = new Dictionary<string, string>();
        }

        public virtual string Name { get; set; }

        public virtual bool Active { get; private set; }

        public virtual IDictionary<string, string> Settings { get; protected set; }

        public virtual RedisQueue Queue { get; protected set; }

        public virtual Func<Job> JobCreator { get; protected set; }

        protected RedisLiveConnection connection;

        private static TriggerWatcher all;

        public static TriggerWatcher All
        {
            get
            {
                if (all == null)
                {
                    var triggerConfigs = Cinchcast.Roque.Core.Configuration.Roque.Settings.Triggers.OfType<TriggerElement>();
                    var triggers = new List<Trigger>();
                    foreach (var triggerConfig in triggerConfigs)
                    {
                        Trigger trigger = (Trigger)Activator.CreateInstance(Type.GetType(triggerConfig.TriggerType));
                        trigger.Name = triggerConfig.Name;
                        trigger.Configure(
                            triggerConfig.Queue,
                            triggerConfig.TargetTypeFullName,
                            triggerConfig.TargetMethodName,
                            triggerConfig.TargetArgument,
                            triggerConfig.Settings.ToDictionary());
                        triggers.Add(trigger);
                    }
                    all = new TriggerWatcher(triggers.ToArray());
                }
                return all;
            }
        }

        public virtual RedisLiveConnection Connection
        {
            get
            {
                if (connection == null)
                {
                    connection = Queue.Connection;
                }
                return connection;
            }
        }

        public async void Configure(string queue, string targetTypeFullName, string targetMethodName, string targetArgument, IDictionary<string, string> settings)
        {
            Settings = settings;
            Queue = (RedisQueue)Roque.Core.Queue.Get(queue);
            connection = Queue.Connection;
            JobCreator = () =>
                {
                    Job job = Job.Create(targetTypeFullName, targetMethodName);
                    if (!string.IsNullOrEmpty(targetArgument))
                    {
                        job.Arguments = new[] { targetArgument };
                    }
                    return job;
                };

            // update trigger info on redis
            var db = connection.GetOpen().GetDatabase();
            await db.HashSetAsync(GetRedisKey("info"), "type", this.GetType().FullName);
            await db.HashSetAsync(GetRedisKey("info"), "queue", queue);
            await db.HashSetAsync(GetRedisKey("info"), "targetTypeFullName", targetTypeFullName);
            await db.HashSetAsync(GetRedisKey("info"), "targetMethodName", targetMethodName);
            await db.HashSetAsync(GetRedisKey("info"), "targetArgument", targetArgument);
            await db.HashSetAsync(GetRedisKey("info"), "lastupdate", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture));
            await db.KeyDeleteAsync(GetRedisKey("settings"));
            foreach (var setting in settings)
            {
                await db.HashSetAsync(GetRedisKey("settings"), setting.Key, setting.Value);
            }
        }

        public virtual DateTime? GetLastExecution()
        {
            try
            {
                var db = Connection.GetOpen().GetDatabase();
                string lastExecutionString = db.HashGet(GetRedisKey(), "lastexecution");

                return String.IsNullOrEmpty(lastExecutionString) ? null : 
                    (DateTime?) DateTime.Parse(lastExecutionString, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error while obtaining trigger {1} last execution time: {0}", ex.Message, Name, ex);
                throw;
            }
        }

        public virtual void Activate()
        {
            if (Active) return;

            OnActivate();

            RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger activated. Type: {0}, Name: {1}", GetType().Name, Name);
        }

        public virtual void Deactivate()
        {
            if (!Active) return;

            OnDeactivate();
            Active = false;
        }

        protected virtual string GetRedisKey(string suffixFormat = null, params object[] parameters)
        {
            return GetRedisKeyForTrigger(Name, suffixFormat, parameters);
        }

        protected virtual string GetRedisKeyForTrigger(string triggerName, string suffixFormat = null, params object[] parameters)
        {
            var key = new StringBuilder(RedisQueue.RedisNamespace);
            key.Append("t:");
            key.Append(triggerName);
            if (!string.IsNullOrEmpty(suffixFormat))
            {
                key.Append(":");
                key.Append(string.Format(suffixFormat, parameters));
            }
            return key.ToString();
        }

        protected virtual void OnActivate()
        {
        }

        protected virtual void OnDeactivate()
        {
        }

        public virtual DateTime? GetNextExecution()
        {
            return GetNextExecution(GetLastExecution());
        }

        protected virtual DateTime? GetNextExecution(DateTime? lastExecution)
        {
            // by default next execution is unknown
            return null;
        }

        public virtual async void Execute(bool force = false)
        {
            DateTime? lastExecution = null;
            DateTime? nextExecution = null;
            try
            {
                lastExecution = GetLastExecution();
                nextExecution = GetNextExecution(lastExecution);
                var db = Connection.GetOpen().GetDatabase();
                await db.HashSetAsync(GetRedisKey(), "nextexecution", 
                    nextExecution == null ? null : nextExecution.Value.ToString("s", CultureInfo.InvariantCulture)
                    );
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error while getting next execution for trigger {1}: {0}", ex.Message, Name, ex);
            }

            if (force || nextExecution != null)
            {
                if (force || nextExecution <= DateTime.UtcNow)
                {
                    ExecuteNow(lastExecution, force);
                }
                else
                {
                    RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger will run at {2} GMT. Type: {0}, Name: {1}", GetType().Name, Name, nextExecution.Value);
                    Thread.Sleep((int)Math.Min(
                        TimeSpan.FromMinutes(1).TotalMilliseconds,
                        Math.Max(nextExecution.Value.Subtract(DateTime.UtcNow).TotalMilliseconds, 1000)));
                }
            }
        }

        protected virtual void ExecuteNow(DateTime? lastExecution, bool force = false)
        {
            bool lockObtained = false;
            IDatabase db = null;
            try
            {
                db = Connection.GetOpen().GetDatabase();
                // semaphore, prevent multiple executions
                lockObtained = db.LockTake(GetRedisKey("executing"), "1", TimeSpan.FromSeconds(5));
                if (!lockObtained)
                {
                    // trigger already executing, abort this execution
                    return;
                }
                if (lastExecution != GetLastExecution())
                {
                    // the trigger got executed right now, abort this execution
                    return;
                }

                if (EnqueueJob())
                {
                    RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger executed. Type: {0}, Name: {1}", GetType().Name, Name);
                    var recentExecution = DateTime.UtcNow;
                    var nextExecution = GetNextExecution(recentExecution);
                    db.HashSet(GetRedisKey(), "lastexecution", recentExecution.ToString("s", CultureInfo.InvariantCulture));
                    db.HashSet(GetRedisKey(), "nextexecution", nextExecution == null ? null :
                        nextExecution.Value.ToString("s", CultureInfo.InvariantCulture));
                    RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger next execution: {2} GMT. Type: {0}, Name: {1}", GetType().Name, Name, nextExecution);
                }
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error while executing trigger {1}: {0}", ex.Message, Name, ex);
            }
            finally
            {
                if (lockObtained)
                {
                    try
                    {
                        db.LockRelease(GetRedisKey("executing"),"0");
                    }
                    catch (Exception ex)
                    {
                        RoqueTrace.Source.Trace(TraceEventType.Error, "Error while releasing trigger {1} lock: {0}", ex.Message, Name, ex);
                    }
                }
            }
        }

        protected virtual bool EnqueueJob()
        {
            try
            {
                var job = JobCreator();
                bool enqueued = Queue.Enqueue(job);
                RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger enqueued job: {0}.{1}. Type: {2}, Name: {3}", job.Target, job.Method, GetType().Name, Name);
                return enqueued;
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error while enqueing trigger {1} job: {0}", ex.Message, Name, ex);
                return false;
            }
        }
    }
}
