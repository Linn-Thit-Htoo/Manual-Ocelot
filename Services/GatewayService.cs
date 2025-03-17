using Manual_Ocelot.Configurations;
using Manual_Ocelot.Constants;
using Newtonsoft.Json;
using System.Text;
using Route = Manual_Ocelot.Configurations.Route;

namespace Manual_Ocelot.Services
{
    public class GatewayService : IGatewayService
    {
        private static readonly object _lock = new();
        private static int _lastUsedIndex = 0;

        public async Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequest(HttpContext httpContext, Route route)
        {
            try
            {
                var requestPath = httpContext.Request.Path.ToString();
                var requestMethod = httpContext.Request.Method;

                lock (_lock)
                {
                    _lastUsedIndex = (_lastUsedIndex + 1) % route.DownstreamHostAndPorts.Count;
                }

                string downstreamHost = route.DownstreamHostAndPorts[_lastUsedIndex].Host;
                int downstreamPort = route.DownstreamHostAndPorts[_lastUsedIndex].Port;

                string upstreamBasePath = route.UpstreamPathTemplate.Replace("{everything}", "");
                string downstreamBasePath = route.DownstreamPathTemplate.Replace("{everything}", "");

                string downstreamPath = requestPath.Replace(upstreamBasePath, downstreamBasePath);

                string downstreamUrl = $"{route.DownstreamScheme}://{downstreamHost}:{downstreamPort}{downstreamPath}";

                var downstreamRequest = new HttpRequestMessage(new HttpMethod(requestMethod), downstreamUrl);

                if (httpContext.Request.Body.CanRead)
                {
                    using var reader = new StreamReader(httpContext.Request.Body);
                    var body = await reader.ReadToEndAsync();
                    downstreamRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                HttpClient httpClient = new();
                var downstreamResponse = await httpClient.SendAsync(downstreamRequest);

                return downstreamResponse;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
