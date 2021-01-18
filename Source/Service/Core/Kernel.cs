using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Service.Models;
using Service.Stuff;
using SharpScheduler.Common;

namespace Service.Core
{
    public partial class ServiceKernel
    {
        protected override async Task Init()
        {
            Start(_serviceOptions.PrefPort);
            Log.Information("Listening on: {0}", Port);

            await Task.WhenAll(_serviceOptions.Handlers.Select(RunHandlerAsync));

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
    }
}