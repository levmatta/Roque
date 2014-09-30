using Cinchcast.Roque.Core;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cinchcast.Roque.Redis
{
    public class RedisLiveConnection
    {
        protected String hosts;

        protected int timeout;

        protected ConnectionMultiplexer  connection;

        protected readonly object syncConnection = new object();

        public RedisLiveConnection(IDictionary<string, string> settings)
        {
            if (!settings.TryGet("hosts", out hosts))
            {
                throw new Exception("Redis host is required");
            }
            this.timeout = settings.Get("timeout", 2000);
        }

        public RedisLiveConnection(string host, int port = 6379, int timeout = 2000)
        {
            hosts = host + ":" + port;
            this.timeout = timeout;
        }

        public RedisLiveConnection(string hosts, int timeout = 2000)
        {
            this.hosts = hosts;
            this.timeout = timeout;
        }

        public virtual ConnectionMultiplexer GetOpen()
        {
            lock (syncConnection)
            {
                if (connection != null && connection.IsConnected)
                {
                    return connection;
                }

                if (connection == null || !connection.IsConnected)
                {
                    try
                    {
                        RoqueTrace.Source.Trace(TraceEventType.Information, "[REDIS] connecting to {0}", hosts);

                        connection = ConnectionMultiplexer.Connect(hosts);

                        RoqueTrace.Source.Trace(TraceEventType.Information, "[REDIS] connected");
                    }
                    catch (Exception ex)
                    {
                        RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] error connecting to {0}, {1}", hosts, ex.Message, ex);
                        throw;
                    }
                }
                return connection;
            }
        }

        public RedisLiveConnection Clone()
        {
            return new RedisLiveConnection(hosts, timeout);
        }
    }
}
