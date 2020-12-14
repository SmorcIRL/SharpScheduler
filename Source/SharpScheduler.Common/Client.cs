using System.Net.Http;
using System.Threading.Tasks;

namespace SharpScheduler.Common
{
    public class Client
    {
        private readonly HttpClient _client;

        public Client()
        {
            _client = new HttpClient();
        }

        public Task<HttpResponseMessage> SendMessageAsync(int port, bool serializeContent, object content, params (string name, object value)[] headers)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, Helper.CreateRootPrefixOnPort(port)).SetupMessage(serializeContent, content, headers);

            return _client.SendAsync(message);
        }
    }
}