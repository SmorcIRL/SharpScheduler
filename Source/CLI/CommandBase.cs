using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using SharpScheduler.Common;

namespace CLI
{
    internal abstract class CommandBase
    {
        [Option("-p", Description = "TCP port used by service")]
        [Required]
        [Range(IPEndPoint.MinPort, IPEndPoint.MaxPort)]
        public int Port { get; set; }

        protected async Task OnExecuteAsync(CommandLineApplication app)
        {
            try
            {
                await ProcessAsync(app);
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException)
                {
                    Console.WriteLine($"Service is unavailable: {ex.Message}");
                }
                else if (ex is TaskCanceledException)
                {
                    Console.WriteLine($"Service is not responding: {ex.Message}");
                }
                else
                {
                    Console.WriteLine(ex);
                }
            }
        }

        protected abstract Task ProcessAsync(CommandLineApplication app);

        protected Task<HttpResponseMessage> SendMessageAsync(bool handleByService, object content, bool serializeContent, params (string, object)[] headers)
        {
            var commandType = GetType();

            var commandAttribute = GetType().GetCustomAttribute<CommandAttribute>();

            if (commandAttribute == null)
            {
                throw new InvalidOperationException($"{commandType.Name} class missing a {nameof(CommandAttribute)}");
            }

            headers = headers.EmptyIfNull().Concat(new (string name, object value)[]
            {
                (Headers.Command, commandAttribute.Name),
                (Headers.CommandTarget, handleByService.ToString())
            }).ToArray();

            return new Client().SendMessageAsync(Port, serializeContent, content, headers.ToArray());
        }
    }

    internal abstract class ServiceCommand : CommandBase
    {
        protected override async Task ProcessAsync(CommandLineApplication app)
        {
            var response = await SendMessageAsyncToService();

            Console.WriteLine(await response.GetMessageAsync());
        }

        protected Task<HttpResponseMessage> SendMessageAsyncToService(object content = null, bool serializeContent = true)
        {
            return SendMessageAsync(true, content, serializeContent);
        }
    }

    internal abstract class HandlerCommand : CommandBase
    {
        [Argument(0, Description = "Handler ID. Try \"info\" command to get list of handlers")]
        [Required]
        [Range(1, long.MaxValue)]
        public long HandlerId { get; set; }

        protected override async Task ProcessAsync(CommandLineApplication app)
        {
            var response = await SendMessageAsyncToHandler();

            Console.WriteLine(await response.GetMessageAsync());
        }

        protected Task<HttpResponseMessage> SendMessageAsyncToHandler(object content = null, bool serializeContent = true)
        {
            return SendMessageAsync(false, content, serializeContent, (Headers.HandlerID, HandlerId.ToString()));
        }
    }
}