using System;
using System.Collections.Generic;
using System.Linq;

namespace WrapperApplication.Models
{
    public class HandlerOptions
    {
        public string[] DisposeArgs { get; set; } = Array.Empty<string>();
        public string[] InitArgs { get; set; } = Array.Empty<string>();
        public TriggersOptions Triggers { get; set; } = new();
    }

    public class TriggersOptions
    {
        public SimpleTrigger[] Simple { get; set; } = Array.Empty<SimpleTrigger>();
        public DatedTrigger[] Dated { get; set; } = Array.Empty<DatedTrigger>();
        public WeeklyTrigger[] Weekly { get; set; } = Array.Empty<WeeklyTrigger>();

        public IEnumerable<TriggerOptions> All => (Simple as IEnumerable<TriggerOptions>).Concat(Dated).Concat(Weekly).ToArray();
    }
}