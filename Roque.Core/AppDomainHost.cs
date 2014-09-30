using System;
using System.Diagnostics;
using System.Threading;

namespace Cinchcast.Roque.Core
{
    /// <summary>
    /// Activate objects on a separate AppDomain.
    /// </summary>
    public abstract class AppDomainHost : IDisposable
    {
        public abstract class Process : MarshalByRefObject
        {
            public virtual void Start(dynamic parameters = null)
            {
                AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
                OnStart(parameters);
            }

            void CurrentDomain_DomainUnload(object sender, EventArgs e)
            {
                AppDomain.CurrentDomain.DomainUnload -= CurrentDomain_DomainUnload;
                OnStop();
            }

            public void Stop()
            {
                OnStop();
            }

            public void GCCollect()
            {
                GC.Collect();
            }

            public abstract void OnStart(dynamic parameters);

            public abstract void OnStop();
        }

        protected Process process;

        protected bool stopping;

        /// <summary>
        /// If true when any *.config or *.dll file changes the Host will be restarted.
        /// </summary>
        public bool RestartOnFileChanges { get; set; }

        /// <summary>
        /// If more than zero, AppDomain allocated memory is monitored. If it exceeds this value the Host will be restarted.
        /// </summary>
        public long RestartIfMemorySizeIsMoreThan { get; set; }

        /// <summary>
        /// AppDomain where workers are running
        /// </summary>
        public AppDomain AppDomain { get; private set; }

        protected Timer timer;

        /// <summary>
        /// Creates a new Host for workers
        /// </summary>
        public AppDomainHost()
        {
            try
            {
                var settings = Roque.Core.Configuration.Roque.Settings;
                RestartOnFileChanges = settings.RestartOnFileChanges;
                RestartIfMemorySizeIsMoreThan = settings.RestartIfMemorySizeIsMoreThan;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error parsing roque configuration block. {0}", ex.Message, ex);
            }
        }

        protected abstract Type GetProcessType();

        /// <summary>
        /// Starts the process of this host.
        /// </summary>
        public void Start(dynamic parameters = null)
        {
            if (AppDomain == null)
            {
                var appDomainSetup = new AppDomainSetup()
                    {
                        ShadowCopyFiles = true.ToString()
                    };
                AppDomain = AppDomain.CreateDomain("AppDomainHost_" + Guid.NewGuid(), null, appDomainSetup);
            }
            if (process != null || stopping)
            {
                throw new Exception("A process in this host is already started");
            }

            Type processType = GetProcessType();

            process = (Process)AppDomain.CreateInstanceAndUnwrap(
                processType.Assembly.FullName,
                processType.FullName);
            Trace.TraceInformation("Starting...");
            process.Start(parameters);
            if (RestartOnFileChanges)
            {
                new FileWatcher().OnConfigOrDllChanges(Restart, true);
            }
            if (timer == null)
            {
                timer = new Timer(TimerTick, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));
            }
        }

        protected void TimerTick(object state)
        {
            if (this.AppDomain != null && process != null && !stopping)
            {
                process.GCCollect();
                if (RestartIfMemorySizeIsMoreThan > 0)
                {
                    AppDomain.MonitoringIsEnabled = true;
                    long bytes = this.AppDomain.MonitoringSurvivedMemorySize;
                    if (bytes > RestartIfMemorySizeIsMoreThan)
                    {
                        Trace.TraceWarning("[MaxMemorySizeCheck] Restarting!. Memory Size exceeded maximum limit ({0}MB)", Math.Round(bytes / 1024.0 / 1024.0, 1));
                        Restart();
                    }
                    else
                    {
                        Trace.TraceInformation("[MaxMemorySizeCheck] Memory Size is normal ({0}MB)", Math.Round(bytes / 1024.0 / 1024.0, 1));
                    }
                }
            }
        }

        /// <summary>
        /// Stops the worker(s) unloading AppDomain
        /// </summary>
        public void Stop()
        {
            if (process == null || stopping)
            {
                return;
            }
            stopping = true;
            var appDomain = AppDomain;
            AppDomain = null;
            process = null;
            process = null;
            Trace.TraceInformation("Stopping...");
            AppDomain.Unload(appDomain);
            Trace.TraceInformation("Stopped");
            stopping = false;
        }

        /// <summary>
        /// Restarts the process on a new AppDomain (unloading the current one)
        /// </summary>
        public void Restart()
        {
            if (stopping)
            {
                return;
            }
            Stop();
            Start();
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// Activate objects on a separate AppDomain.
    /// </summary>
    /// <typeparam name="TProcess"></typeparam>
    public class AppDomainHost<TProcess> : AppDomainHost
        where TProcess : AppDomainHost.Process
    {
        protected override Type GetProcessType()
        {
            return typeof(TProcess);
        }
    }
}
