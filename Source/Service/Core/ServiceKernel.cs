using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Serilog;
using Service.Models;
using Service.Stuff;
using SharpScheduler.Common;

namespace Service.Core
{
    public partial class ServiceKernel : Kernel
    {
        private static ServiceOptions _serviceOptions;
        private readonly Client _client;
        private readonly Dictionary<long, WrapperProcess> _wrappers;
        private readonly object _wrappersLock;

        public ServiceKernel(IOptions<ServiceOptions> options)
        {
            _serviceOptions = options.Value;

            _client = new Client();
            _wrappers = new Dictionary<long, WrapperProcess>();
            _wrappersLock = new object();
        }

        private async Task<string> RunHandlerAsync(HandlerOptions options)
        {
            var wrapperProcess = new WrapperProcess(options);
            long wrapperProcessID = wrapperProcess.ID;

            string retstr;

            Log.Information("Running handler[{0}]: \"{1}\"", wrapperProcessID, options.Path);

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
    }
}