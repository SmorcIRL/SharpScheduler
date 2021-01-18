using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using System.Threading;
using ConsoleTables;
using SharpScheduler.Common;
using WrapperApplication.Models;

namespace WrapperApplication.Scheduler
{
    public class SimpleScheduler : IScheduler
    {
        private static readonly TimeSpan IntervalMin = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan IntervalMax = TimeSpan.FromHours(1);
        private static readonly TimeSpan QueueRange = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxDelayToAddNote = TimeSpan.FromSeconds(5);
        private static readonly ConsoleTableOptions TableOptions = new()
        {
            EnableCount = false,
            Columns = new[] {"ID", "Command", "Args count", "Ticks left", "Interval", "Next tick"}
        };


        private readonly object _notesLock;
        private readonly ISubject<(string Command, string[] Args)> _ticksSubject;
        private readonly Timer _timer;

        private Dictionary<long, TriggerNote> _notes;

        public SimpleScheduler()
        {
            _notesLock = new object();
            _timer = new Timer(CheckNotes, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _ticksSubject = Subject.Synchronize(new Subject<(string Command, string[] Args)>());
            _notes = new Dictionary<long, TriggerNote>();

            TickStream = _ticksSubject;
        }

        public IObservable<(string Command, string[] Args)> TickStream { get; }

        public void Add(TriggerOptions options)
        {
            AddRange(new[] {options});
        }

        public void AddRange(IEnumerable<TriggerOptions> infos)
        {
            lock (_notesLock)
            {
                var now = DateTime.Now;

                foreach (var info in infos)
                {
                    var note = new TriggerNote(info);

                    if (now - note.NextTick < MaxDelayToAddNote)
                    {
                        _notes[note.Id] = note;
                    }
                }

                UpdateTimer();
            }
        }

        public void Remove(long triggerId)
        {
            lock (_notesLock)
            {
                if (_notes.Remove(triggerId))
                {
                    UpdateTimer();
                }
                else
                {
                    throw new Exception("Wrong trigger id");
                }
            }
        }

        public void RemoveAll()
        {
            lock (_notesLock)
            {
                _notes.Clear();

                UpdateTimer();
            }
        }

        public string GetStat()
        {
            lock (_notesLock)
            {
                if (!_notes.Any())
                {
                    return "No active triggers\n";
                }

                var table = new ConsoleTable(TableOptions);

                foreach (var note in _notes.Values.OrderBy(x => x.Id))
                {
                    table.AddRow(note.Id, note.Info.Command, note.Info.Args.Length, note.TicksLeft, note.Interval, note.NextTick);
                }

                return table.ToString();
            }
        }

        private void UpdateTimer()
        {
            lock (_notesLock)
            {
                FilterAndSortNotes();

                if (!_notes.Any())
                {
                    SetTimersNextTick(IntervalMax);
                    return;
                }

                var now = DateTime.Now;
                var nextTick = _notes.First().Value.NextTick;

                SetTimersNextTick(nextTick - now);
            }

            void FilterAndSortNotes()
            {
                _notes = _notes.Where(x => x.Value.Tickable).OrderBy(x => x.Value.NextTick)
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            void SetTimersNextTick(TimeSpan delay)
            {
                _timer.Change(Clamp(delay), Timeout.InfiniteTimeSpan);

                static TimeSpan Clamp(TimeSpan value)
                {
                    if (value < IntervalMin)
                    {
                        return IntervalMin;
                    }

                    return value > IntervalMax ? IntervalMax : value;
                }
            }
        }

        private void CheckNotes(object _ = null)
        {
            var now = DateTime.Now;
            var tickQueue = new Queue<TriggerNote>();

            lock (_notesLock)
            {
                foreach (var (_, value) in _notes)
                {
                    if (value.NextTick - now < QueueRange)
                    {
                        tickQueue.Enqueue(value);
                    }
                    else
                    {
                        break;
                    }
                }

                foreach (var note in tickQueue)
                {
                    note.Tick();

                    _ticksSubject.OnNext(note.Info);
                }

                UpdateTimer();
            }
        }

        private class TriggerNote
        {
            private static readonly ObjectIDGenerator IdGenerator = new();

            public TriggerNote(TriggerOptions options)
            {
                Id = IdGenerator.GetId(this, out _);
                Info = (options.Command, options.Args.EmptyIfNull());

                options.GetParams(out var firstTick, out var interval, out int maxTicks);

                NextTick = firstTick;

                if (interval <= TimeSpan.Zero)
                {
                    Interval = IntervalMin;
                    TicksLeft = 1;
                }
                else
                {
                    Interval = interval < IntervalMin ? IntervalMin : interval;
                    TicksLeft = maxTicks;
                }
            }

            public long Id { get; }
            public (string Command, string[] Args) Info { get; }

            public DateTime NextTick { get; private set; }
            public TimeSpan Interval { get; }
            public int TicksLeft { get; private set; }

            public bool Tickable => TicksLeft != 0;

            public void Tick()
            {
                if (TicksLeft > 0)
                {
                    TicksLeft--;
                }

                NextTick = NextTick.Add(Interval);
            }
        }
    }
}