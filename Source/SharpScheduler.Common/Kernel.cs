using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SharpScheduler.Common
{
    public abstract class Kernel : Listener
    {
        private readonly CancellationTokenSource _selfDisposingCTS;
        private readonly List<Task> _shutdownReasons;
        private KernelStatus _status;

        public Kernel()
        {
            _selfDisposingCTS = new CancellationTokenSource();
            _shutdownReasons = new List<Task>();
            _status = KernelStatus.Initing;
            ShutDownReason = "unknown reason";
        }
        protected Task InitTask { get; set; }
        protected string ShutDownReason { get; set; }

        protected abstract Task Init();
        protected abstract Task OnInitFailed(Exception ex);
        protected abstract Task DisposeAsync(int firstIndex);
        protected abstract Task<string> HandleRequest(HttpListenerRequest request, HttpListenerResponse response, string defaultMessage);

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            InitTask = Init();

            try
            {
                await InitTask;
            }
            catch (Exception ex)
            {
                _status = KernelStatus.ShuttingDown;

                await OnInitFailed(ex);

                return;
            }

            _status = KernelStatus.Running;

            _shutdownReasons.Add(stoppingToken.WaitForCancelerationAsync());
            _shutdownReasons.Add(_selfDisposingCTS.Token.WaitForCancelerationAsync());
            _shutdownReasons.AddRange(GetShutdownReasons());

            int firstIndex = _shutdownReasons.IndexOf(await Task.WhenAny(_shutdownReasons));

            _status = KernelStatus.ShuttingDown;

            await DisposeAsync(firstIndex);
        }

        protected virtual Task[] GetShutdownReasons()
        {
            return Array.Empty<Task>();
        }

        protected void Stop()
        {
            _selfDisposingCTS.Cancel();
        }

        protected override async Task HandleContextAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var statusCode = HttpStatusCode.OK;
            string handlingMessage = "OK";

            try
            {
                switch (_status)
                {
                    case KernelStatus.Initing:
                    {
                        await InitTask;

                        goto case KernelStatus.Running;
                    }
                    case KernelStatus.Running:
                    {
                        handlingMessage = await HandleRequest(request, response, handlingMessage);
                        break;
                    }
                    case KernelStatus.ShuttingDown:
                    {
                        throw new Exception(ShutDownReason);
                    }
                }
            }
            catch (Exception ex)
            {
                statusCode = HttpStatusCode.BadRequest;
                handlingMessage = ex.Message;
            }

            response.SetStatusCode(statusCode);
            await response.SetMessageAsync(handlingMessage);
        }

        private enum KernelStatus
        {
            Initing,
            Running,
            ShuttingDown
        }
    }
}