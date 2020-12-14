using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Service.Core;

namespace Service.Project
{
    public class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly ServiceKernel _kernel;

        public Worker(IHostApplicationLifetime appLifetime, ServiceKernel kernel)
        {
            _appLifetime = appLifetime;
            _kernel = kernel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _kernel.StartAsync(stoppingToken);

            _appLifetime.StopApplication();
        }
    }
}