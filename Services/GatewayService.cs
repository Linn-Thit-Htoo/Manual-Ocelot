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
        private readonly Dictionary<string, int> _activeConnections = new();

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

        public async Task<HttpResponseMessage> ProcesssLeastConnectionLoadBalancingRequest(HttpContext httpContext, Route route)
        {
            try
            {
                var requestPath = httpContext.Request.Path.ToString();
                var requestMethod = httpContext.Request.Method;

                var leastConnectionHost = GetLeastConnectionDownstreamHost(route.DownstreamHostAndPorts) ?? throw new Exception("Unexpected error occured.");

                IncrementActiveConnections(leastConnectionHost.Host, leastConnectionHost.Port);

                string upstreamBasePath = route.UpstreamPathTemplate.Replace("{everything}", "");
                string downstreamBasePath = route.DownstreamPathTemplate.Replace("{everything}", "");

                string downstreamPath = requestPath.Replace(upstreamBasePath, downstreamBasePath);

                string downstreamUrl = $"{route.DownstreamScheme}://{leastConnectionHost.Host}:{leastConnectionHost.Port}{downstreamPath}";

                var downstreamRequest = new HttpRequestMessage(new HttpMethod(requestMethod), downstreamUrl);

                if (httpContext.Request.Body.CanRead)
                {
                    using var reader = new StreamReader(httpContext.Request.Body);
                    var body = await reader.ReadToEndAsync();
                    downstreamRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                HttpClient httpClient = new();
                var downstreamResponse = await httpClient.SendAsync(downstreamRequest);

                DecrementActiveConnections(leastConnectionHost.Host, leastConnectionHost.Port);

                return downstreamResponse;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private Downstreamhostandport GetLeastConnectionDownstreamHost(List<Downstreamhostandport> downstreamHosts)
        {
            foreach (var hostPort in downstreamHosts)
            {
                var key = $"{hostPort.Host}:{hostPort.Port}";
                if (!_activeConnections.ContainsKey(key))
                {
                    _activeConnections[key] = 0;
                }
            }

            return downstreamHosts
                .OrderBy(hostPort => _activeConnections[$"{hostPort.Host}:{hostPort.Port}"])
                .FirstOrDefault()!;
        }

        private void IncrementActiveConnections(string host, int port)
        {
            var key = $"{host}:{port}";
            if (!_activeConnections.ContainsKey(key))
            {
                _activeConnections[key] = 0;
            }
            _activeConnections[key]++;
        }

        private void DecrementActiveConnections(string host, int port)
        {
            var key = $"{host}:{port}";
            if (_activeConnections.ContainsKey(key) && _activeConnections[key] > 0)
            {
                _activeConnections[key]--;
            }
        }
    }
}
