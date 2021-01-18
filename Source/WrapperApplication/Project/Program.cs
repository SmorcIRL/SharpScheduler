using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using SharpScheduler.Common;
using WrapperApplication.Core;
using WrapperApplication.Scheduler;

namespace WrapperApplication.Project
{
    public class Program
    {
        private static readonly WrapperProcessArgs Args = new();
        private static Action _toLog;

        public static void Main(string[] args)
        {
            ProcessArgs(args);

            SetupLogger();

            Run();
        }

        private static void ProcessArgs(string[] args)
        {
            var builder = new ConfigurationBuilder();
            builder.AddCommandLine(args);
            builder.Build().Bind(Args);

            _toLog += () => Log.Information(LogStrings.Started);

            try
            {
                string currentDir = Path.GetDirectoryName(Args.HandlerPath);

                Directory.SetCurrentDirectory(currentDir!);
            }
            catch (Exception ex)
            {
                _toLog += () => Log.Error("Cannot set current directory: \"{0}\"", ex);
            }

            _toLog += () => Log.Information("Current directory: \"{0}\"", Directory.GetCurrentDirectory());
        }

        private static void SetupLogger()
        {
            var configuration = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext();

            Args.Log ??= GetDefaultLogFileName();

            try
            {
                Log.Logger = configuration
                    .WriteTo.File(Args.Log)
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                _toLog += () => Log.Error("Cannot use log file \"{0}\": {1}", Args.Log, ex);

                Args.Log = GetDefaultLogFileName();

                Log.Logger = configuration
                    .WriteTo.File(Args.Log)
                    .CreateLogger();
            }

            _toLog += () => Log.Information("Used log file: \"{0}\"", Args.Log);

            static string GetDefaultLogFileName()
            {
                return $"{Path.GetFileNameWithoutExtension(Names.LogFileName)}_{Environment.ProcessId}{Path.GetExtension(Names.LogFileName)}";
            }
        }

        private static void Run()
        {
            try
            {
                _toLog();
                CreateHostBuilder().Build().Run();
            }
            catch (Exception ex)
            {
                Log.Error("Unhandled exception: {ex}", ex);
            }
            finally
            {
                Log.CloseAndFlush();
            }

            static IHostBuilder CreateHostBuilder()
            {
                return Host.CreateDefaultBuilder()
                    .ConfigureServices((_, services) =>
                    {
                        services.AddSingleton(Args);
                        services.AddSingleton<IScheduler, SimpleScheduler>();
                        services.AddSingleton<Kernel, WrapperKernel>();
                        services.AddHostedService<SingleWorker>();
                    })
                    .UseSerilog();
            }
        }
    }
}