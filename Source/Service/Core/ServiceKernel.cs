using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Serilog;
using Service.Models;
using Service.Stuff;
using SharpScheduler.Common;

namespace Service.Core
{
    public class ServiceKernel : Kernel
    {
        private static ServiceOptions _options;
        private readonly Client _client;
        private readonly Dictionary<long, WrapperProcess> _wrappers;
        private readonly object _wrappersLock;

        public ServiceKernel(IOptions<ServiceOptions> options)
        {
            _options = options.Value;

            _client = new Client();
            _wrappers = new Dictionary<long, WrapperProcess>();
            _wrappersLock = new object();
        }

        private async Task<string> RunHandlerAsync(HandlerOptions options)
        {
            var wrapperProcess = new WrapperProcess(options);
            long wrapperProcessID = wrapperProcess.ID;

            string retstr;

            Log.Information("Running handler[{0}] ({1})", wrapperProcessID, options.Path);

            try
            {
                wrapperProcess.Start();

                await Task.Delay(Const.WrapperInitTimeout);

                var initResult = await _client.SendMessageAsync(wrapperProcess.Port, true, null, (Headers.Command, HandlerCommands.InitInfo));
                string message = await initResult.GetMessageAsync();

                if (initResult.IsSuccessStatusCode)
                {
                    AddWrapper(wrapperProcess);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await wrapperProcess.ProcessDeathWaiting;
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }

                        RemoveWrapper(wrapperProcess.ID);
                    });

                    retstr = "Handler[{0}] has started successfully: {1}";
                    Log.Information(retstr, wrapperProcessID, message);
                }
                else
                {
                    retstr = "Handler[{0}] failed to start: {1}";
                    Log.Error(retstr, wrapperProcessID, message);
                }

                retstr = string.Format(retstr, wrapperProcessID, message);
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException)
                {
                    retstr = "Handler[{0}] is unavailable: {1}";
                    Log.Error(retstr, wrapperProcessID, ex);
                }
                else
                {
                    retstr = "Exception while trying to run handler[{0}]: {1}";
                    Log.Error(retstr, wrapperProcessID, ex);
                }

                retstr = string.Format(retstr, wrapperProcessID, ex);
            }

            return retstr;
        }

        private class WrapperProcess
        {
            private static readonly ObjectIDGenerator IdGenerator = new();
            private readonly CancellationTokenSource _cts;
            private readonly Process _process;

            public WrapperProcess(HandlerOptions options)
            {
                Port = Helper.GetFreeTcpPort();
                ID = IdGenerator.GetId(this, out _);
                _cts = new CancellationTokenSource();
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo(_options.DotnetPath)
                    {
                        Arguments = GetArgs(),
                        CreateNoWindow = true
                    }
                };

                string GetArgs()
                {
                    string args = $"{_options.WrapperPath} " +
                                  $"{nameof(WrapperProcessArgs.ServicePID)}={ServiceOptions.ServicePID} " +
                                  $"{nameof(WrapperProcessArgs.HandlerPort)}={Port} " +
                                  $"{nameof(WrapperProcessArgs.HandlerPath)}={options.Path}";

                    if (options.Schedule != null)
                    {
                        args += $" {nameof(WrapperProcessArgs.Schedule)}={options.Schedule}";
                    }

                    if (options.Log != null)
                    {
                        args += $" {nameof(WrapperProcessArgs.Log)}={options.Log}";
                    }

                    return args;
                }
            }

            public Task ProcessDeathWaiting { get; private set; }
            public int Port { get; }
            public long ID { get; }
            public bool IsRunning => !ProcessDeathWaiting.IsCompleted;

            public void Start()
            {
                _process.Start();
                ProcessDeathWaiting = Task.Run(async () =>
                {
                    await _process.WaitForExitAsync(_cts.Token);

                    Kill();

                    Log.Information("Handler[{0}] was stopped", ID);
                });
            }
            private void Kill(bool cancelDeathWaiting = false)
            {
                if (cancelDeathWaiting)
                {
                    _cts.Cancel();
                }

                try
                {
                    _process.Kill();
                }
                catch (Exception ex)
                {
                    Log.Error("Exception while trying to kill handler[{0}] process: {1}", ID, ex);
                }
                finally
                {
                    _process.Close();
                }
            }
        }


        #region Kernel

        protected override async Task Init()
        {
            StartListening(_options.PrefPort);
            Log.Information("Listening on: {0}", Port);

            await Task.WhenAll(_options.Handlers.Select(RunHandlerAsync));

            Log.Information("Ready to handle CLI requests");
        }
        protected override Task OnInitFailed(Exception ex)
        {
            ShutDownReason = $"Init failed: {ex}";

            Log.Error(ShutDownReason);

            return Task.CompletedTask;
        }
        protected override async Task DisposeAsync(int firstIndex)
        {
            Log.Information("Service is shutting down");

            if (firstIndex == 1)
            {
                Dictionary<long, Task<HttpResponseMessage>> handlerStopTasks;

                lock (_wrappersLock)
                {
                    handlerStopTasks = _wrappers
                        .Values
                        .Where(x => x.IsRunning)
                        .ToDictionary(x => x.ID, x => _client.SendMessageAsync(x.Port, true, null, (Headers.Command, HandlerCommands.Stop)));
                }

                try
                {
                    await Task.WhenAll(handlerStopTasks.Values);
                }
                catch (Exception ex)
                {
                    Log.Error("Exception while trying to stop all handlers: {0}", ex);
                }

                await Task.Delay(Const.WrappersAutoRemoveTimeout);

                Log.Information("{0}/{1} handlers where closed successfully", handlerStopTasks.Count, handlerStopTasks.Count(x => x.Value.IsCompletedSuccessfully));
            }
        }
        protected override async Task<string> HandleRequest(HttpListenerRequest request, HttpListenerResponse response, string defaultMessage)
        {
            string command = request.GetHeaderValue(Headers.Command, "Wrong request to service, command is not specified");

            bool forService = request.GetHeaderValue<bool>(Headers.CommandTarget, "Wrong request to service, command target is not specified");

            if (forService)
            {
                switch (command)
                {
                    case ServiceCommands.Stop:
                    {
                        Stop();
                        break;
                    }
                    case ServiceCommands.Info:
                    {
                        var builder = new StringBuilder();
                        Dictionary<long, Task<HttpResponseMessage>> handlerInfoTasks;

                        lock (_wrappersLock)
                        {
                            RefreshWrappers();

                            builder.Append($"\nTotal active handlers: {_wrappers.Count}\n");

                            handlerInfoTasks = _wrappers
                                .Values
                                .Where(x => x.IsRunning)
                                .ToDictionary(x => x.ID, x => _client.SendMessageAsync(x.Port, true, null, (Headers.Command, HandlerCommands.Info)));
                        }

                        try
                        {
                            await Task.WhenAll(handlerInfoTasks.Values);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Exception while gathering info from handlers: {0}", ex);
                        }

                        foreach ((long key, var task) in handlerInfoTasks.OrderBy(x => x.Key))
                        {
                            builder.Append($"\nHandler[{key}]:\n");

                            try
                            {
                                string message = await (await task).GetMessageAsync();

                                builder.Append($"{message}");
                            }
                            catch (Exception ex)
                            {
                                builder.Append($"Request failed with exception: {ex}");
                            }
                        }

                        return builder.ToString();
                    }
                    case ServiceCommands.RunHandler:
                    {
                        var options = await request.GetContentAsync<HandlerOptions>();

                        return await RunHandlerAsync(options);
                    }
                    default:
                    {
                        throw new Exception($"Service cannot handle command: {command}");
                    }
                }
            }
            else
            {
                long handlerID = request.GetHeaderValue<long>(Headers.HandlerID, "Handler ID is not specified");

                if (!TryGetWrapper(handlerID, out var wrapper))
                {
                    throw new Exception($"No handler for ID = {handlerID}");
                }

                if (!wrapper.IsRunning)
                {
                    throw new Exception("Handler is closing and cannot accept requests");
                }

                string content = await request.GetContentAsync();
                var handlerResponse = await _client.SendMessageAsync(wrapper.Port, false, content, (Headers.Command, command));

                string message = await handlerResponse.GetMessageAsync();

                if (handlerResponse.IsSuccessStatusCode)
                {
                    return message;
                }

                throw new Exception(message);
            }

            return defaultMessage;
        }

        #endregion

        #region Synced

        private bool TryGetWrapper(long id, out WrapperProcess wrapperProcess)
        {
            lock (_wrappersLock)
            {
                return _wrappers.TryGetValue(id, out wrapperProcess);
            }
        }

        private void RemoveWrapper(long id)
        {
            lock (_wrappersLock)
            {
                _wrappers.Remove(id);
            }
        }

        private void AddWrapper(WrapperProcess wrapperProcess)
        {
            lock (_wrappersLock)
            {
                _wrappers[wrapperProcess.ID] = wrapperProcess;
            }
        }

        private void RefreshWrappers()
        {
            lock (_wrappersLock)
            {
                foreach (var wrapper in _wrappers.Values.ToArray())
                {
                    if (!wrapper.IsRunning)
                    {
                        _wrappers.Remove(wrapper.ID);
                    }
                }
            }
        }

        #endregion
    }
}