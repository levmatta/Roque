﻿using System.Configuration;

namespace Cinchcast.Roque.Core.Configuration
{
    using System;
    using System.Linq;

    /// <summary>
    /// Worker config section
    /// </summary>
    public class WorkerElement : ConfigurationSection
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return this["name"] as string; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("queue", IsRequired = true)]
        public string Queue
        {
            get { return this["queue"] as string; }
            set { this["queue"] = value; }
        }

        [ConfigurationProperty("autoStart", DefaultValue = false)]
        public bool AutoStart
        {
            get { return (bool)this["autoStart"]; }
            set { this["autoStart"] = value; }
        }

        [ConfigurationProperty("tooManyErrors", DefaultValue = 10)]
        [IntegerValidator(ExcludeRange = false, MaxValue = 1000, MinValue = 1)]
        public int TooManyErrors
        {
            get { return (int)this["tooManyErrors"]; }
            set { this["tooManyErrors"] = value; }
        }

        [ConfigurationProperty("tooManyErrorsRetrySeconds", DefaultValue = 30)]
        public int TooManyErrorsRetrySeconds
        {
            get { return (int)this["tooManyErrorsRetrySeconds"]; }
            set { this["tooManyErrorsRetrySeconds"] = value; }
        }

        [ConfigurationProperty("subscribers")]
        public SubscriberCollection Subscribers
        {
            get
            {
                return this["subscribers"] as SubscriberCollection;
            }
        }
    }
}
