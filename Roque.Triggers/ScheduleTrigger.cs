using System;
using System.Linq;
using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Triggers
{
    /// <summary>
    /// Trigger that executes based on a schedule (supports cron syntax)
    /// </summary>
    public class ScheduleTrigger : Trigger
    {
        protected Func<DateTime?, DateTime?> NextExecutionGetter;

        protected override DateTime? GetNextExecution(DateTime? lastExecution)
        {
            if (NextExecutionGetter == null)
            {
                var schedule = Schedule.Create(Settings.Get<string, string, string>("schedule"));
                NextExecutionGetter = schedule.GetNextExecution;
            }

            return NextExecutionGetter(lastExecution);
        }
    }
}
