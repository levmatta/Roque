using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Redis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using StackExchange.Redis;

    /// <summary>
    /// Redis-based implementation of a <see cref="Queue"/>
    /// </summary>
    public class RedisQueue : Queue, IQueueWithInProgressData
    {
        /// <summary>
        /// prefix for queues names in Redis
        /// </summary>
        public static string RedisNamespace = "roque:";

        private RedisLiveConnection connection;

        public RedisLiveConnection Connection
        {
            get
            {
                if (connection == null)
                {
                    connection = new RedisLiveConnection(Settings);
                }
                return connection;
            }
        }

        protected readonly object syncConnection = new object();

        protected IDictionary<string, string[]> subscribersCache = new Dictionary<string, string[]>();

        protected ISubscriber subscribedToSubscribersChangesChannel;

        protected DateTime subscribersCacheLastClear = DateTime.Now;

        public static TimeSpan DefaultSubscribersCacheExpiration = TimeSpan.FromMinutes(60);

        public TimeSpan? SubscribersCacheExpiration { get; set; }

        public RedisQueue(string name, IDictionary<string, string> setings)
            : base(name, setings)
        {
        }

        protected virtual string GetRedisKey(string suffixFormat = null, params object[] parameters)
        {
            return GetRedisKeyForQueue(Name, suffixFormat, parameters);
        }

        protected virtual string GetRedisKeyForQueue(string queueName, string suffixFormat = null, params object[] parameters)
        {
            var key = new StringBuilder(RedisNamespace);
            key.Append("q:");
            key.Append(queueName);
            if (!string.IsNullOrEmpty(suffixFormat))
            {
                key.Append(":");
                key.Append(string.Format(suffixFormat, parameters));
            }
            return key.ToString();
        }

        protected string GetWorkerKey(Worker worker)
        {
            return string.Format("w_{0}_{1}", worker.Name, worker.ID);
        }

        protected override async void EnqueueJson(string data)
        {
            Connection.GetOpen().GetDatabase().ListInsertBeforeAsync(GetRedisKey(), 0, data);
        }

        protected override string DequeueJson(Worker worker, int timeoutSeconds)
        {
            var db = Connection.GetOpen().GetDatabase();

            // move job from queue to worker in progress
            string data = db.ListRightPopLeftPush(GetRedisKey(), GetRedisKey("worker:{0}:inprogress", GetWorkerKey(worker)));

            if (!string.IsNullOrEmpty(data))
            {
                try
                {
                    db.HashSet(GetRedisKey("worker:{0}:state", GetWorkerKey(worker)), "currentstart", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture));
                }
                catch (Exception ex)
                {
                    RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] error registering job start: {0}", ex.Message, ex);
                }
            }
            return data;
        }

        protected override string PeekJson(out long length)
        {
            var db = Connection.GetOpen().GetDatabase();

            string data = db.ListGetByIndex(GetRedisKey(), -1);
            if (data == null)
            {
                length = 0;
            }
            else
            {
                length = db.ListLength(GetRedisKey());
            }
            return data;
        }

        protected override DateTime? DoGetTimeOfLastJobCompleted()
        {
            var db = Connection.GetOpen().GetDatabase();

            string data = db.HashGet(GetRedisKey("state"), "lastcomplete");
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }
            else
            {
                return DateTime.Parse(data, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            }
        }

        public string GetInProgressJson(Worker worker)
        {
            var db = Connection.GetOpen().GetDatabase();
            string data = db.ListGetByIndex(GetRedisKey("worker:{0}:inprogress", GetWorkerKey(worker)), 0);
            if (data != null)
            {
                try
                {
                    db.HashSet(GetRedisKey("worker:{0}:state", GetWorkerKey(worker)), "currentstart", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture));
                }
                catch (Exception ex)
                {
                    RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] error registering in progress job start: {0}", ex.Message, ex);
                }
            }
            return data;
        }

        public async void JobCompleted(Worker worker, Job job, bool failed)
        {
            try
            {
                // LVM transaction ??
                var db = Connection.GetOpen().GetDatabase();
                string json = await db.ListLeftPopAsync(GetRedisKey("worker:{0}:inprogress", GetWorkerKey(worker)));
                if (failed)
                {
                    await db.ListInsertBeforeAsync(GetRedisKey("failed"), 0, json);
                }
                await db.HashDeleteAsync(GetRedisKey("worker:{0}:state", GetWorkerKey(worker)), "currentstart");
                await db.HashSetAsync(GetRedisKey("worker:{0}:state", GetWorkerKey(worker)), "lastcomplete", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture));
                await db.HashSetAsync(GetRedisKey("state"), "lastcomplete", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] error registering job completion: {0}", ex.Message, ex);
                throw;
            }
        }

        public void ClearSubscribersCache()
        {
            subscribersCache.Clear();
        }

        protected override void DoReportEventSubscription(string sourceQueue, string target, string eventName)
        {
            var db = Connection.GetOpen().GetDatabase();
            var added = db.SortedSetAdd(GetRedisKeyForQueue(sourceQueue, "events:{0}:{1}:subscribers", target, eventName), Name, 0);
            if (added)
            {
                db.PublishAsync(GetRedisKeyForQueue(sourceQueue, "events:subscriberschanges"), "+" + target + ":" + eventName);
                RoqueTrace.Source.Trace(TraceEventType.Information, "[REDIS] Queue {0} subscribed to events {1}:{2} events on queue {3}", Name, target, eventName, sourceQueue);
            }
        }

        public override string[] GetSubscribersForEvent(string target, string eventName)
        {
            string[] subscribers;
            string eventKey = target + ":" + eventName;

            if (subscribedToSubscribersChangesChannel != null && !subscribedToSubscribersChangesChannel.IsConnected())
            {
                // connection dropped, create a new one
                subscribedToSubscribersChangesChannel = null;
                ClearSubscribersCache();
            }
            if (subscribedToSubscribersChangesChannel == null)
            {
                var connection = Connection.GetOpen();
                subscribedToSubscribersChangesChannel = connection.GetSubscriber();
                subscribedToSubscribersChangesChannel.Subscribe(GetRedisKey("events:subscriberschanges"), (message, bytes) =>
                {
                    RoqueTrace.Source.Trace(TraceEventType.Information, "[REDIS] Subscribers added to {0}, clearing subscribers cache", Name);
                    ClearSubscribersCache();
                });
                RoqueTrace.Source.Trace(TraceEventType.Verbose, "[REDIS] Listening for subscribers changes on queue {0}", Name);
            }

            if (DateTime.Now.Subtract(subscribersCacheLastClear) > (SubscribersCacheExpiration ?? DefaultSubscribersCacheExpiration))
            {
                ClearSubscribersCache();
            }

            if (!subscribersCache.TryGetValue(eventKey, out subscribers))
            {
                var db = Connection.GetOpen().GetDatabase();
                subscribers = db.SortedSetRangeByScore(GetRedisKey("events:{0}:subscribers", eventKey), 0, -1)
                    .Select(value => (String) value).ToArray();
                subscribersCache[eventKey] = subscribers;
            }
            return subscribers;
        }

        protected override async void EnqueueJsonEvent(string data, string target, string eventName)
        {
            var db = Connection.GetOpen().GetDatabase();

            var subscribers = GetSubscribersForEvent(target, eventName);

            if (subscribers == null || subscribers.Length == 0)
            {
                RoqueTrace.Source.Trace(TraceEventType.Verbose, "No subscriber for this event, enqueue omitted. Event: {0}:{1}, Queue:{2}", target, eventName, Name);
            }
            else
            {
                foreach (var subscriber in subscribers)
                {
                    try
                    {
                        db.ListInsertBeforeAsync(GetRedisKeyForQueue(subscriber), 0,data);
                    }
                    catch (Exception ex)
                    {
                        RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] Error enqueuing event on {0}. Event: {1}:{2}. {3}", subscriber, target, eventName, ex.Message, ex);
                    }
                }
                if (RoqueTrace.Source.Switch.ShouldTrace(TraceEventType.Verbose))
                {
                    RoqueTrace.Source.Trace(TraceEventType.Verbose, "Event published to queues: {0}. Event: {1}:{2}", string.Join(", ", subscribers), target, eventName);
                }
            }
        }

        public override IDictionary<string, string[]> GetSubscribers()
        {
            var db = Connection.GetOpen().GetDatabase();
            var keys = db.HashKeys(GetRedisKey("events:*:subscribers"));
            var keyRegex = new Regex("^" + GetRedisKey("events:(.*):subscribers$"));
            IDictionary<string, string[]> subscribers = new Dictionary<string, string[]>();
            foreach (var key in keys)
            {
                var eventKey = keyRegex.Match(key).Groups[1].Value;
                var targets = db.SortedSetRangeByRank((String) key).ToArray();
                if (targets.Length > 0)
                {
                    subscribers[eventKey] = targets.Select(val => (String) val).ToArray();
                }
            }
            return subscribers;
        }
    }
}
