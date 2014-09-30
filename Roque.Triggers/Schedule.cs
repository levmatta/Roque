using System;
using System.Collections.Generic;
using System.Linq;

namespace Cinchcast.Roque.Triggers
{
    /// <summary>
    /// Creates Schedules for ScheduleTrigger, parses cron expressions
    /// </summary>
    public class Schedule
    {
        private readonly HashSet<byte> minute;
        private readonly HashSet<byte> hour;
        private readonly HashSet<byte> dayOfMonth;
        private readonly HashSet<byte> monthOfYear;
        private readonly HashSet<byte> dayOfWeek;

        private Schedule(HashSet<byte> minute, HashSet<byte> hour, HashSet<byte> dayOfMonth, HashSet<byte> monthOfYear, HashSet<byte> dayOfWeek)
        {
            this.minute = minute;
            this.hour = hour;
            this.dayOfMonth = dayOfMonth;
            this.monthOfYear = monthOfYear;
            this.dayOfWeek = dayOfWeek;
        }

        private static HashSet<byte> ByteSet(params byte[] values)
        {
            return new HashSet<byte>(values.AsEnumerable());
        }

        private static HashSet<byte> ByteSetRange(byte from, byte to, byte step = 1)
        {
            var hashSet = new HashSet<byte>();
            if (step < 1)
            {
                step = 1;
            }
            for (var b = Math.Min(from, to); b <= Math.Max(from, to); b += step)
            {
                hashSet.Add(b);
            }
            return hashSet;
        }

        private static HashSet<byte> ByteSet(string cronset, byte minValue, byte maxValue)
        {
            var rangeAndStep = cronset.Split('/');
            var step = (byte)(rangeAndStep.Length > 1 ? byte.Parse(rangeAndStep[1]) : 1);
            if (step < 1)
            {
                step = 1;
            }
            if (rangeAndStep[0] == "*")
            {
                return ByteSetRange(minValue, maxValue, step);
            }
            var values = rangeAndStep[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var hashSet = new HashSet<byte>();
            foreach (var value in values)
            {
                var valueRange = value.Split('-');
                if (valueRange.Length > 1)
                {
                    byte from = byte.Parse(valueRange[0]);
                    byte to = byte.Parse(valueRange[1]);
                    for (var b = Math.Min(from, to); b <= Math.Max(from, to); b += step)
                    {
                        hashSet.Add(b);
                    }
                }
                else
                {
                    hashSet.Add(byte.Parse(valueRange[0]));
                }
            }
            return hashSet;
        }

        /// <summary>
        /// Creates a Schedule using cron syntax
        /// </summary>
        /// <param name="schedule"></param>
        /// <returns></returns>
        public static Schedule Create(string schedule)
        {
            var parts = schedule.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return new Schedule(
                parts.Length > 0 ? ByteSet(parts[0], 0, 59) : null,
                parts.Length > 1 ? ByteSet(parts[1], 0, 23) : null,
                parts.Length > 2 ? ByteSet(parts[2], 1, 31) : null,
                parts.Length > 3 ? ByteSet(parts[3], 1, 12) : null,
                parts.Length > 4 ? ByteSet(parts[4], 0, 6) : null
                );
        }

        public bool IsValidExecutionTime(DateTime time)
        {
            return IsValidExecutionTime((byte)time.Month, (byte)time.Day, (byte)time.DayOfWeek, (byte)time.Hour, (byte)time.Minute);
        }

        public bool IsValidExecutionTime(byte month, byte day, byte weekDay, byte hour, byte minute)
        {
            if (this.monthOfYear != null && !this.monthOfYear.Contains(month))
            {
                return false;
            }
            if (this.dayOfMonth != null && !this.dayOfMonth.Contains(day))
            {
                return false;
            }
            if (this.dayOfWeek != null && !this.dayOfWeek.Contains(weekDay))
            {
                return false;
            }

            // the day is valid

            if (this.hour != null && !this.hour.Contains(hour))
            {
                return false;
            }
            if (this.minute != null && !this.minute.Contains(minute))
            {
                return false;
            }

            // the time is valid

            return true;
        }

        public bool IsValidExecutionDay(DateTime time)
        {
            return IsValidExecutionDay((byte)time.Month, (byte)time.Day, (byte)time.DayOfWeek);
        }

        public bool IsValidExecutionDay(byte month, byte day, byte weekDay)
        {
            if (this.monthOfYear != null && !this.monthOfYear.Contains(month))
            {
                return false;
            }
            if (this.dayOfMonth != null && !this.dayOfMonth.Contains(day))
            {
                return false;
            }
            if (this.dayOfWeek != null && !this.dayOfWeek.Contains(weekDay))
            {
                return false;
            }

            // the day is valid

            return true;
        }

        public DateTime? GetNextExecution(DateTime? lastExecution)
        {
            var last = (lastExecution ?? DateTime.UtcNow);
            var next = last.AddMinutes(1);
            next = new DateTime(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0);

            if (IsValidExecutionDay(next))
            {
                while (!IsValidExecutionTime(next))
                {
                    next = next.AddMinutes(1);
                    if (next.Date != last.Date)
                    {
                        break;                        
                    }
                }
                if (IsValidExecutionTime(next))
                {
                    return next;
                }
            }

            // no more times on the same day, look for the next day

            var daysAdded = 0;
            while (!IsValidExecutionDay(next))
            {
                if (daysAdded > 1500)
                {
                    // no valid day found (eg. looking for Feb 31)
                    return null;
                }
                next = next.AddDays(1);
                daysAdded++;
            }
            next = new DateTime(next.Year, next.Month, next.Day,
                this.hour == null ? 0 : this.hour.Min(),
                this.minute == null ? 0 : this.minute.Min(),
                0);

            return next;
        }
    }
}
