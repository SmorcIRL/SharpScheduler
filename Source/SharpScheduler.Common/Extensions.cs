using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SharpScheduler.Common
{
    public static class Extensions
    {
        public static async Task WaitForCancelerationAsync(this CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (TaskCanceledException)
            {
            }
        }
        public static T[] EmptyIfNull<T>(this T[] array)
        {
            return array ?? Array.Empty<T>();
        }
        private static T ChangeType<T>(this object obj)
        {
            return (T) Convert.ChangeType(obj, typeof(T));
        }

        #region HTTP

        public static void SetBadRequestStatusCode(this HttpListenerResponse response)
        {
            response.SetStatusCode(HttpStatusCode.BadRequest);
        }
        public static void SetOKStatusCode(this HttpListenerResponse response)
        {
            response.SetStatusCode(HttpStatusCode.OK);
        }
        public static void SetStatusCode(this HttpListenerResponse response, HttpStatusCode statusCode)
        {
            response.StatusCode = (int) statusCode;
        }

        public static HttpRequestMessage SetupMessage(this HttpRequestMessage requestMessage, bool serializeContent, object content, params (string, object)[] headers)
        {
            if (content != null)
            {
                string stringContent = serializeContent ? JsonConvert.SerializeObject(content) : content.ToString();

                requestMessage.Content = new StringContent(stringContent!);
            }

            foreach ((string name, var value) in headers.EmptyIfNull())
            {
                requestMessage.Headers.Add(name, value.ToString());
            }

            return requestMessage;
        }

        public static async Task<T> GetContentAsync<T>(this HttpListenerRequest request)
        {
            string content = await request.GetContentAsync();

            return JsonConvert.DeserializeObject<T>(content);
        }

        public static async Task<string> GetContentAsync(this HttpListenerRequest request)
        {
            await using var body = request.InputStream;
            using var reader = new StreamReader(body, request.ContentEncoding);

            return await reader.ReadToEndAsync();
        }

        public static T GetHeaderValue<T>(this HttpListenerRequest request, string key, string exceptionMessage)
        {
            try
            {
                string stored = request.Headers.GetValues(key)!.First();

                return stored.ChangeType<T>();
            }
            catch
            {
                throw new Exception(exceptionMessage);
            }
        }
        public static string GetHeaderValue(this HttpListenerRequest request, string key, string exceptionMessage)
        {
            try
            {
                return request.Headers.GetValues(key)!.First();
            }
            catch
            {
                throw new Exception(exceptionMessage);
            }
        }

        public static async Task SetMessageAsync(this HttpListenerResponse response, string content)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer.AsMemory(0, buffer.Length));
        }
        public static async Task<string> GetMessageAsync(this HttpResponseMessage responseMessage)
        {
            return await responseMessage.Content.ReadAsStringAsync();
        }

        #endregion
    }

    public static class Helper
    {
        private static readonly HashSet<int> UsedPorts = new();
        private static readonly object UsedPortsLock = new();

        public static string CreateRootPrefixOnPort(int port)
        {
            return $"http://localhost:{port}/";
        }
        public static TimeSpan GetDelay(DateTime now, DayOfWeek dayOfWeek, DateTime dataTime)
        {
            TimeSpan delay;
            var time = dataTime.TimeOfDay;

            if (now.DayOfWeek == dayOfWeek)
            {
                delay = time - now.TimeOfDay;

                if (now.TimeOfDay >= time)
                {
                    delay += TimeSpan.FromDays(7);
                }
            }
            else
            {
                var nowDate = now.Date;

                while (nowDate.DayOfWeek != dayOfWeek)
                {
                    nowDate = nowDate.AddDays(1);
                }

                delay = nowDate + time - now;
            }

            return delay;
        }
        public static int GetFreeTcpPort()
        {
            lock (UsedPortsLock)
            {
                while (true)
                {
                    var listener = new TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                    int port = ((IPEndPoint) listener.LocalEndpoint).Port;
                    listener.Stop();

                    if (UsedPorts.Add(port))
                    {
                        return port;
                    }
                }
            }
        }
    }
}