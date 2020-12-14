using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using WrapperApplication.Core;

namespace WrapperApplication.Project
{
    public class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly WrapperKernel _kernel;

        public Worker(IHostApplicationLifetime appLifetime, WrapperKernel kernel)
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