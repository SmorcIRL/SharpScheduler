using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Options;
using SharpScheduler.Common;

namespace Service.Models
{
    public class ServiceOptions
    {
        public static int ServicePID { get; } = Environment.ProcessId;

        public int PrefPort { get; set; } = -1;
        public string WrapperPath { get; set; } = Names.WrapperAppName;
        public string DotnetPath { get; set; } = Names.DotnetUtilName;
        public HandlerOptions[] Handlers { get; set; } = Array.Empty<HandlerOptions>();
    }

    public class ServiceOptionsValidator : IValidateOptions<ServiceOptions>
    {
        public ValidateOptionsResult Validate(string name, ServiceOptions options)
        {
            try
            {
                CheckWrapperApplicationExistence();
                CheckDotnetCLIInstallation();
            }
            catch (Exception ex)
            {
                return ValidateOptionsResult.Fail(ex.Message);
            }

            return ValidateOptionsResult.Success;

            void CheckWrapperApplicationExistence()
            {
                try
                {
                    _ = AssemblyName.GetAssemblyName(options.WrapperPath);
                }
                catch (FileNotFoundException)
                {
                    throw new Exception($"{Names.WrapperAppName} cannot be found at: \"{options.WrapperPath}\"");
                }
                catch (BadImageFormatException)
                {
                    throw new Exception($"Wrapper application file specified as {options.WrapperPath} is not an assembly");
                }
                catch
                {
                    throw new Exception($"Cannot load {Names.WrapperAppName}, that is required to run handlers");
                }
            }

            void CheckDotnetCLIInstallation()
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(options.DotnetPath)
                    {
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };

                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    throw new Exception("Unable to run dotnet CLI, that is required to run handlers\n" +
                                        "Please install it if it not done yet: https://docs.microsoft.com/en-us/dotnet/core/tools/\n" +
                                        $"Exception message: {ex.Message}");
                }
            }
        }
    }
}