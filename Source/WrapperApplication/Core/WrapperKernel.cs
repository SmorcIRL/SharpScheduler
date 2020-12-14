using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using SharpScheduler.Common;
using SmorcIRL.SharpScheduler.Handlers;
using WrapperApplication.Models;
using WrapperApplication.Scheduler;

namespace WrapperApplication.Core
{
    public class WrapperKernel : Kernel
    {
        private readonly Handler _handler;
        private readonly WrapperProcessArgs _processArgs;
        private readonly IScheduler _scheduler;
        private HandlerOptions _handlerOptions;

        private IDisposable _schedulerSub;
        private Task _waitForServiceDeath;

        public WrapperKernel(Handler handler, IScheduler scheduler, WrapperProcessArgs processArgs)
        {
            _handler = handler;
            _scheduler = scheduler;
            _processArgs = processArgs;
        }

        private async Task PreDisposeJobAsync()
        {
            Log.Information($"Handler is shutting down due to: {ShutDownReason}");

            _schedulerSub?.Dispose();

            // To handle active requests
            await Task.Delay(TimeSpan.FromSeconds(3));

            StopListening();
        }

        #region Kernel

        protected override async Task Init()
        {
            Log.Information("Starting initialization");

            StartListening(_processArgs.HandlerPort);

            // In this case, the service will not be able to find this handler
            if (Port != _processArgs.HandlerPort)
            {
                throw new Exception("Cannot run on required TCP port");
            }

            Log.Information("Listening on: {0}", Port);

            _waitForServiceDeath = Process.GetProcessById(_processArgs.ServicePID).WaitForExitAsync();

            using (var reader = new StreamReader(_processArgs.Schedule))
            {
                string json = await reader.ReadToEndAsync();

                _handlerOptions = JsonConvert.DeserializeObject<HandlerOptions>(json);
            }

            await _handler.Init(_processArgs.HandlerPath, _handlerOptions.InitArgs);

            _schedulerSub = _scheduler.TickStream.Subscribe(x => _ = _handler.HandleSchedulerTick(x));

            _scheduler.AddRange(_handlerOptions.Triggers.All);
        }
        protected override async Task OnInitFailed(Exception ex)
        {
            if (ex is JsonReaderException)
            {
                ShutDownReason = $"Init failed because of schedule file parsing error: {ex.Message}";
            }
            else
            {
                ShutDownReason = $"Init failed: {ex}";
            }

            await PreDisposeJobAsync();
        }
        protected override async Task DisposeAsync(int firstIndex)
        {
            ShutDownReason = firstIndex switch
            {
                0 => "application host request",
                1 => "service request",
                2 => "service process death",
                3 => $"triggering {nameof(IHandler.RequestDispose)}",
                _ => ShutDownReason
            };

            await PreDisposeJobAsync();

            await _handler.Dispose(_handlerOptions.DisposeArgs);
        }
        protected override async Task<string> HandleRequest(HttpListenerRequest request, HttpListenerResponse response, string defaultMessage)
        {
            string command = request.GetHeaderValue(Headers.Command, "Wrong request to handler, command is not specified");

            switch (command)
            {
                case HandlerCommands.InitInfo:
                {
                    await InitTask;

                    break;
                }
                case HandlerCommands.Stop:
                {
                    string[] args = await request.GetContentAsync<string[]>();

                    if (args != null)
                    {
                        _handlerOptions.DisposeArgs = args;
                    }

                    Stop();

                    break;
                }
                case HandlerCommands.StopTrigger:
                {
                    long triggerID = await request.GetContentAsync<long>();

                    _scheduler.Remove(triggerID);

                    break;
                }
                case HandlerCommands.Handle:
                {
                    var info = await request.GetContentAsync<CommandInfo>();

                    (string value, bool returns) = await _handler.Handle(info.Command, info.Args);

                    if (returns)
                    {
                        return value;
                    }

                    break;
                }
                case HandlerCommands.ExtendSchedule:
                {
                    var options = await request.GetContentAsync<TriggersOptions>();

                    _scheduler.AddRange(options.All);

                    return _scheduler.GetStat();
                }
                case HandlerCommands.Info:
                {
                    return _scheduler.GetStat();
                }
                case HandlerCommands.GetLog:
                {
                    using (var reader = new StreamReader(_processArgs.Log))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
                default:
                {
                    throw new Exception($"Handler cannot handle command: {command}");
                }
            }

            return defaultMessage;
        }

        protected override Task[] GetShutdownReasons()
        {
            return new[] {_waitForServiceDeath, _handler.WaitForDisposeRequest()};
        }

        #endregion
    }
}