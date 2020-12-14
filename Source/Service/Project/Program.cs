using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Service.Core;
using Service.Models;
using SharpScheduler.Common;

namespace Service.Project
{
    public class Program
    {
        public static void Main()
        {
            SetupLogger();

            Run();
        }

        private static void SetupLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
#if DEBUG
                .WriteTo.Debug()
                .WriteTo.Console()
#endif
                .WriteTo.File(Names.LogFileName)
                .CreateLogger();
        }

        private static void Run()
        {
            try
            {
                Log.Information(LogStrings.Started);
                CreateHostBuilder().Build().Run();
            }
            catch (Exception ex)
            {
                Log.Error("Unhandled exception: {ex}", ex);
            }
            finally
            {
                Log.Information(LogStrings.Finished);
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder()
        {
            var builder = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((_, config) => { config.AddJsonFile(Names.ServiceConfig, true); })
                .ConfigureServices((context, services) =>
                {
                    services.AddOptions<ServiceOptions>().Bind(context.Configuration);
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ServiceOptions>, ServiceOptionsValidator>());
                    services.AddSingleton<ServiceKernel>();
                    services.AddHostedService<Worker>();
                })
                .UseSerilog();

            return MakeCrossPlatform(builder);

            static IHostBuilder MakeCrossPlatform(IHostBuilder hostBuilder)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return hostBuilder.UseWindowsService();
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return hostBuilder.UseSystemd();
                }

                throw new NotSupportedException();
            }
        }
    }
}