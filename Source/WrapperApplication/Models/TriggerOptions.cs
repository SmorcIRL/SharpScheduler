using System;
using System.Threading;
using SharpScheduler.Common;

namespace WrapperApplication.Models
{
    public abstract class TriggerOptions
    {
        public string Command { get; set; }
        public string[] Args { get; set; }

        public abstract void GetParams(out DateTime firstTick, out TimeSpan interval, out int maxTicks);
    }

    public class SimpleTrigger : TriggerOptions
    {
        public TimeSpan Delay { get; set; }
        public TimeSpan Interval { get; set; } = Timeout.InfiniteTimeSpan;
        public int Ticks { get; set; } = -1;

        public override void GetParams(out DateTime firstTick, out TimeSpan interval, out int maxTicks)
        {
            firstTick = DateTime.Now + Delay;
            interval = Interval;
            maxTicks = Ticks;
        }
    }

    public class DatedTrigger : TriggerOptions
    {
        public DateTime Date { get; set; }

        public override void GetParams(out DateTime firstTick, out TimeSpan interval, out int maxTicks)
        {
            firstTick = Date;
            interval = Timeout.InfiniteTimeSpan;
            maxTicks = 1;
        }
    }

    public class WeeklyTrigger : TriggerOptions
    {
        private static readonly TimeSpan Week = TimeSpan.FromDays(7);

        public DayOfWeek WeekDay { get; set; } = DateTime.Now.DayOfWeek;
        public DateTime Time { get; set; } = DateTime.Now;
        public int Ticks { get; set; } = -1;

        public override void GetParams(out DateTime firstTick, out TimeSpan interval, out int maxTicks)
        {
            var now = DateTime.Now;
            firstTick = now.Add(Helper.GetDelay(now, WeekDay, Time));
            interval = Week;
            maxTicks = Ticks;
        }
    }
}