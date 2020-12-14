using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace CLI
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            await CommandLineApplication.ExecuteAsync<RootCommand>(args);
        }
    }
}