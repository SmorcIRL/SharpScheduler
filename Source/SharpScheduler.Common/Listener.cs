using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SharpScheduler.Common
{
    public abstract class Listener
    {
        private readonly CancellationTokenSource _listeningCTS;
        private HttpListener _listener;

        protected Listener()
        {
            _listeningCTS = new CancellationTokenSource();
        }

        protected int Port { get; private set; }

        protected void Start(int preferredPort = -1)
        {
            StartOnFreePort(preferredPort);

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_listeningCTS.IsCancellationRequested)
                    {
                        var getContext = _listener.GetContextAsync();
                        var waitForStop = _listeningCTS.Token.WaitForCancelerationAsync();

                        if (await Task.WhenAny(getContext, waitForStop) == waitForStop)
                        {
                            return;
                        }

                        _ = HandleContextAndCloseAsync(await getContext);
                    }
                }
                finally
                {
                    _listener.Close();
                    _listener = null;
                    Port = -1;
                }
            });
        }
        protected void Close()
        {
            _listeningCTS.Cancel();
        }

        private void StartOnFreePort(int preferredPort)
        {
            int port = preferredPort;

            while (true)
            {
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add(Helper.CreateRootPrefixOnPort(port));
                    listener.Start();

                    _listener = listener;
                    Port = port;

                    break;
                }
                catch
                {
                    port = Helper.GetFreeTcpPort();
                }
            }
        }

        protected abstract Task HandleContextAsync(HttpListenerContext context);
        private async Task HandleContextAndCloseAsync(HttpListenerContext context)
        {
            try
            {
                await HandleContextAsync(context);
            }
            finally
            {
                context.Response.Close();
            }
        }
    }
}