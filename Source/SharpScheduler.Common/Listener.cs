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

        public int Port { get; private set; }

        public void StartListening(int preferredPort = -1)
        {
            StartListenerOnFreePort(preferredPort);

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_listeningCTS.IsCancellationRequested)
                    {
                        var getContext = _listener.GetContextAsync();
                        var waitForStop = _listeningCTS.Token.WaitForCancelerationAsync();

                        var first = await Task.WhenAny(getContext, waitForStop);

                        if (first == waitForStop)
                        {
                            return;
                        }

                        var context = await getContext;

                        _ = Task.Run(async () =>
                        {
                            await HandleContextAsync(context);

                            context.Response.Close();
                        });
                    }
                }
                finally
                {
                    _listener.Close();

                    _listener.Prefixes.Clear();

                    _listener = null;

                    Port = -1;
                }
            });
        }

        public void StopListening()
        {
            _listeningCTS.Cancel();
        }

        private void StartListenerOnFreePort(int preferredPort)
        {
            int port = preferredPort;

            while (true)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add(Helper.CreateRootPrefixOnPort(port));

                    _listener.Start();

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
    }
}