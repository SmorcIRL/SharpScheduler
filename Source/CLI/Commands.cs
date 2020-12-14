using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using SharpScheduler.Common;

namespace CLI
{
    [Command(Name = Names.RootCommand, Description = "\n===========================================================" +
                                                     "\n    CLI for https://github.com/SmorcIRL/SharpScheduler" +
                                                     "\n===========================================================")]
    // Commands for service
    [Subcommand(typeof(ServiceStopCommand))]
    [Subcommand(typeof(ServiceInfoCommand))]
    [Subcommand(typeof(RunHandlerCommand))]
    // Commands for handlers
    [Subcommand(typeof(HandleStopCommand))]
    [Subcommand(typeof(HandleInfoCommand))]
    [Subcommand(typeof(StopTriggerCommand))]
    [Subcommand(typeof(ExtendScheduleCommand))]
    [Subcommand(typeof(HandleCommand))]
    [Subcommand(typeof(GetLogCommand))]
    public class RootCommand
    {
        protected Task OnExecuteAsync(CommandLineApplication app)
        {
            app.ShowHelp();
            return Task.CompletedTask;
        }
    }

    #region Service

    [Command(Name = ServiceCommands.Stop, Description = "Stop service")]
    internal class ServiceStopCommand : ServiceCommand
    {
    }

    [Command(Name = ServiceCommands.Info, Description = "Get info about active handlers and their triggers")]
    internal class ServiceInfoCommand : ServiceCommand
    {
    }

    [Command(Name = ServiceCommands.RunHandler, Description = "Start new handler")]
    internal class RunHandlerCommand : ServiceCommand
    {
        [Argument(1, Description = "Path to the handler DLL")]
        [Required]
        [LegalFilePath]
        public string Path { get; set; }

        [Argument(2, Description = "Schedule file path")]
        [LegalFilePath]
        public string Schedule { get; set; }

        [Argument(3, Description = "Log file path")]
        [LegalFilePath]
        public string Log { get; set; }

        protected override async Task ProcessAsync(CommandLineApplication app)
        {
            var response = await SendMessageAsyncToService(new {Path, Schedule, Log});

            Console.WriteLine(await response.GetMessageAsync());
        }
    }

    #endregion

    #region Handler

    [Command(Name = HandlerCommands.Stop, Description = "Stop selected handler")]
    internal class HandleStopCommand : HandlerCommand
    {
        [Argument(1, Description = "Arguments, passed into the handler's DisposeAsync(). If not specified, the arguments specified in the schedule file will be used")]
        public string[] Args { get; set; }

        protected override async Task ProcessAsync(CommandLineApplication app)
        {
            var response = await SendMessageAsyncToHandler(Args);

            Console.WriteLine(await response.GetMessageAsync());
        }
    }

    [Command(Name = HandlerCommands.Info, Description = "Get list of handlers's active triggers")]
    internal class HandleInfoCommand : HandlerCommand
    {
    }

    [Command(Name = HandlerCommands.StopTrigger, Description = "Stop selected trigger")]
    internal class StopTriggerCommand : HandlerCommand
    {
        [Argument(1, Description = "Trigger ID to stop, Try \"infoh\" command to get list of handlers's active triggers")]
        [Required]
        [Range(1, long.MaxValue)]
        public long TriggerId { get; set; }

        protected override async Task ProcessAsync(CommandLineApplication app)
        {
            var response = await SendMessageAsyncToHandler(TriggerId);

            Console.WriteLine(await response.GetMessageAsync());
        }
    }

    [Command(Name = HandlerCommands.ExtendSchedule, Description = "Extend handler's list of triggers using \"schedule\"-like file")]
    internal class ExtendScheduleCommand : HandlerCommand
    {
        [Argument(1, Description = "Path to the file")]
        [Required]
        [LegalFilePath]
        [FileExists]
        public string Path { get; set; }

        protected override async Task ProcessAsync(CommandLineApplication app)
        {
            string json;

            using (var reader = new StreamReader(Path))
            {
                json = await reader.ReadToEndAsync();
            }

            var response = await SendMessageAsyncToHandler(json, false);

            Console.WriteLine(await response.GetMessageAsync());
        }
    }

    [Command(Name = HandlerCommands.Handle, Description = "Handle command and return result without creating a trigger")]
    internal class HandleCommand : HandlerCommand
    {
        [Argument(1, "Command to handle")]
        [Required]
        public string Command { get; set; }

        [Argument(2, "Command arguments")]
        public string[] Args { get; set; }

        protected override async Task ProcessAsync(CommandLineApplication app)
        {
            var response = await SendMessageAsyncToHandler(new {Command, Args});

            Console.WriteLine(await response.GetMessageAsync());
        }
    }

    [Command(Name = HandlerCommands.GetLog, Description = "Get copy of the local handler's log")]
    internal class GetLogCommand : HandlerCommand
    {
        [Argument(1, Description = "Path to save log's copy")]
        [Required]
        [LegalFilePath]
        public string Path { get; set; }

        protected override async Task ProcessAsync(CommandLineApplication app)
        {
            var response = await SendMessageAsyncToHandler();
            string message = await response.GetMessageAsync();

            if (response.IsSuccessStatusCode)
            {
                await using (var writer = new StreamWriter(Path))
                {
                    await writer.WriteAsync(message);
                }

                Console.WriteLine($"Log saved to: \"{Path}\"");
            }
            else
            {
                Console.WriteLine($"Cannot get log: {message}");
            }
        }
    }

    #endregion
}