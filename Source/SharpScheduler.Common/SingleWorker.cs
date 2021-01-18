using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace SharpScheduler.Common
{
    public class SingleWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly Kernel _kernel;

        public SingleWorker(IHostApplicationLifetime appLifetime, Kernel kernel)
        {
            _appLifetime = appLifetime;
            _kernel = kernel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _kernel.StartAsync(stoppingToken);
            }
            finally
            {
                _appLifetime.StopApplication();
            }
        }
    }
}