using System;
using System.Collections.Generic;
using WrapperApplication.Models;

namespace WrapperApplication.Scheduler
{
    public interface IScheduler
    {
        IObservable<(string Command, string[] Args)> TickStream { get; }

        void Add(TriggerOptions options);

        void AddRange(IEnumerable<TriggerOptions> options);

        void Remove(long triggerId);

        void RemoveAll();

        string GetStat();
    }
}