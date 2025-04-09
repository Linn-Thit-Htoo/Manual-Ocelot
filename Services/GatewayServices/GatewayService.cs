using Manual_Ocelot.Models;
using Manual_Ocelot.Utils;

namespace Manual_Ocelot.Services.GatewayServices;

public class GatewayService : IGatewayService
{
    private static readonly object _lock = new();
    private static int _lastUsedIndex = 0;
    private readonly ConcurrentDictionary<string, int> _activeConnections = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _appDbContext;

    public GatewayService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory serviceScopeFactory
    )
    {
        _httpClientFactory = httpClientFactory;

        var scope = serviceScopeFactory.CreateScope();
        _appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public async Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequest(
        HttpContext httpContext,
        Route route
    )
    {
        try
        {
            var requestPath = httpContext.Request.Path.ToString();
            var requestMethod = httpContext.Request.Method;

            lock (_lock)
            {
                _lastUsedIndex = (_lastUsedIndex + 1) % route.DownstreamHostAndPorts!.Length;
            }

            string downstreamHost = route.DownstreamHostAndPorts[_lastUsedIndex].Host;
            int downstreamPort = route.DownstreamHostAndPorts[_lastUsedIndex].Port;

            string upstreamBasePath = route.UpstreamPathTemplate.Replace("{everything}", "");
            string downstreamBasePath = route.DownstreamPathTemplate.Replace("{everything}", "");

            string downstreamPath = requestPath.Replace(upstreamBasePath, downstreamBasePath);

            string downstreamUrl =
                $"{route.DownstreamScheme}://{downstreamHost}:{downstreamPort}{downstreamPath}";
            if (httpContext.Request.QueryString.HasValue)
            {
                downstreamUrl += httpContext.Request.QueryString;
            }

            var downstreamRequest = new HttpRequestMessage(
                new HttpMethod(requestMethod),
                downstreamUrl
            );

            if (httpContext.Request.Body.CanRead)
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();
                downstreamRequest.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json"
                );
            }

            HttpClient httpClient = _httpClientFactory.CreateClient();
            var downstreamResponse = await httpClient.SendAsync(downstreamRequest);

            return downstreamResponse;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequestV1(
        HttpContext httpContext,
        Route route
    )
    {
        try
        {
            var requestPath = httpContext.Request.Path.ToString();
            var requestMethod = httpContext.Request.Method;

            string downstreamHost = string.Empty;
            int downstreamPort = default;

            if (!string.IsNullOrEmpty(route.ServiceName))
            {
                var instances = await _appDbContext
                    .Tbl_ServiceRegistries.AsNoTracking()
                    .Where(x => x.ServiceName == route.ServiceName)
                    .ToListAsync();

                _lastUsedIndex = Interlocked.Increment(ref _lastUsedIndex) % instances.Count;

                downstreamHost = instances[_lastUsedIndex].Host;
                downstreamPort = instances[_lastUsedIndex].Port;
            }
            else
            {
                _lastUsedIndex =
                    Interlocked.Increment(ref _lastUsedIndex)
                    % route.DownstreamHostAndPorts!.Length;

                downstreamHost = route.DownstreamHostAndPorts[_lastUsedIndex].Host;
                downstreamPort = route.DownstreamHostAndPorts[_lastUsedIndex].Port;
            }

            string upstreamBasePath = route.UpstreamPathTemplate.Replace("{everything}", "");
            string downstreamBasePath = route.DownstreamPathTemplate.Replace("{everything}", "");

            string downstreamPath = requestPath.Replace(upstreamBasePath, downstreamBasePath);

            string downstreamUrl =
                $"{route.DownstreamScheme}://{downstreamHost}:{downstreamPort}{downstreamPath}";
            if (httpContext.Request.QueryString.HasValue)
            {
                downstreamUrl += httpContext.Request.QueryString;
            }

            var downstreamRequest = new HttpRequestMessage(
                new HttpMethod(requestMethod),
                downstreamUrl
            );

            if (httpContext.Request.Body.CanRead)
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();
                downstreamRequest.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json"
                );
            }

            int timeoutInSeconds = route.TimeoutValue ?? 100;

            using CancellationTokenSource cancellationTokenSource = new(
                TimeSpan.FromSeconds(timeoutInSeconds)
            );
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            HttpClient httpClient = _httpClientFactory.CreateClient();
            HttpResponseMessage downstreamResponse = await httpClient.SendAsync(
                downstreamRequest,
                cancellationToken
            );

            return downstreamResponse;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequestV2(
        HttpContext httpContext,
        Route route
    )
    {
        try
        {
            var requestPath = httpContext.Request.Path.ToString();
            var requestMethod = httpContext.Request.Method;

            string downstreamHost = string.Empty;
            int downstreamPort = default;

            if (!string.IsNullOrEmpty(route.ServiceName))
            {
                HttpClient serviceDiscoveryClient = _httpClientFactory.CreateClient("ServiceDiscoveryClient");
                HttpRequestMessage serviceDiscoveryRequest = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(serviceDiscoveryClient.BaseAddress!, $"/api/ServiceDiscovery/DiscoverService?serviceName={route.ServiceName}"),
                };
                HttpResponseMessage serviceDiscoveryResponse = await serviceDiscoveryClient.SendAsync(serviceDiscoveryRequest);
                serviceDiscoveryResponse.EnsureSuccessStatusCode();

                string responseJson = await serviceDiscoveryResponse.Content.ReadAsStringAsync();
                var model = JsonConvert.DeserializeObject<Result<List<ServiceDiscoveryModel>>>(responseJson) ?? throw new ArgumentException("No service found.");
                var instances = model!.Data;

                _lastUsedIndex = Interlocked.Increment(ref _lastUsedIndex) % instances!.Count;

                downstreamHost = instances[_lastUsedIndex].HostName;
                downstreamPort = instances[_lastUsedIndex].Port;
            }
            else
            {
                _lastUsedIndex =
                    Interlocked.Increment(ref _lastUsedIndex)
                    % route.DownstreamHostAndPorts!.Length;

                downstreamHost = route.DownstreamHostAndPorts[_lastUsedIndex].Host;
                downstreamPort = route.DownstreamHostAndPorts[_lastUsedIndex].Port;
            }

            string upstreamBasePath = route.UpstreamPathTemplate.Replace("{everything}", "");
            string downstreamBasePath = route.DownstreamPathTemplate.Replace("{everything}", "");

            string downstreamPath = requestPath.Replace(upstreamBasePath, downstreamBasePath);

            string downstreamUrl =
                $"{route.DownstreamScheme}://{downstreamHost}:{downstreamPort}{downstreamPath}";
            if (httpContext.Request.QueryString.HasValue)
            {
                downstreamUrl += httpContext.Request.QueryString;
            }

            var downstreamRequest = new HttpRequestMessage(
                new HttpMethod(requestMethod),
                downstreamUrl
            );

            if (httpContext.Request.Body.CanRead)
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();
                downstreamRequest.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json"
                );
            }

            int timeoutInSeconds = route.TimeoutValue ?? 100;

            using CancellationTokenSource cancellationTokenSource = new(
                TimeSpan.FromSeconds(timeoutInSeconds)
            );
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            HttpClient httpClient = _httpClientFactory.CreateClient();
            HttpResponseMessage downstreamResponse = await httpClient.SendAsync(
                downstreamRequest,
                cancellationToken
            );

            return downstreamResponse;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<HttpResponseMessage> ProcessLeastConnectionLoadBalancingRequest(
        HttpContext httpContext,
        Route route
    )
    {
        try
        {
            var requestPath = httpContext.Request.Path.ToString();
            var requestMethod = httpContext.Request.Method;

            var leastConnectionHost =
                GetLeastConnectionDownstreamHost(route.DownstreamHostAndPorts!)
                ?? throw new Exception("Unexpected error occured.");

            IncrementActiveConnections(leastConnectionHost.Host, leastConnectionHost.Port);

            string upstreamBasePath = route.UpstreamPathTemplate.Replace("{everything}", "");
            string downstreamBasePath = route.DownstreamPathTemplate.Replace("{everything}", "");

            string downstreamPath = requestPath.Replace(upstreamBasePath, downstreamBasePath);

            string downstreamUrl =
                $"{route.DownstreamScheme}://{leastConnectionHost.Host}:{leastConnectionHost.Port}{downstreamPath}";
            if (httpContext.Request.QueryString.HasValue)
            {
                downstreamUrl += httpContext.Request.QueryString;
            }

            var downstreamRequest = new HttpRequestMessage(
                new HttpMethod(requestMethod),
                downstreamUrl
            );

            if (httpContext.Request.Body.CanRead)
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();
                downstreamRequest.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json"
                );
            }

            HttpClient httpClient = _httpClientFactory.CreateClient();
            var downstreamResponse = await httpClient.SendAsync(downstreamRequest);

            DecrementActiveConnections(leastConnectionHost.Host, leastConnectionHost.Port);

            return downstreamResponse;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<HttpResponseMessage> ProcessLeastConnectionLoadBalancingRequestV1(
        HttpContext httpContext,
        Route route
    )
    {
        try
        {
            var requestPath = httpContext.Request.Path.ToString();
            var requestMethod = httpContext.Request.Method;

            Downstreamhostandport? leastConnectionHost = null;

            if (!string.IsNullOrEmpty(route.ServiceName))
            {
                leastConnectionHost = await GetLeastConnectionDownstreamHostFromServiceRegistry(
                    route.ServiceName
                );
            }
            else if (route.DownstreamHostAndPorts is { Length: > 0 })
            {
                leastConnectionHost = GetLeastConnectionDownstreamHost(
                    route.DownstreamHostAndPorts
                );
            }

            if (leastConnectionHost is null)
                throw new Exception("No available downstream instances.");

            IncrementActiveConnections(leastConnectionHost.Host, leastConnectionHost.Port);

            string upstreamBasePath = route.UpstreamPathTemplate.Replace("{everything}", "");
            string downstreamBasePath = route.DownstreamPathTemplate.Replace("{everything}", "");

            string downstreamPath = requestPath.Replace(upstreamBasePath, downstreamBasePath);

            string downstreamUrl =
                $"{route.DownstreamScheme}://{leastConnectionHost.Host}:{leastConnectionHost.Port}{downstreamPath}";
            if (httpContext.Request.QueryString.HasValue)
            {
                downstreamUrl += httpContext.Request.QueryString;
            }

            var downstreamRequest = new HttpRequestMessage(
                new HttpMethod(requestMethod),
                downstreamUrl
            );

            if (httpContext.Request.Body.CanRead)
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();
                downstreamRequest.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json"
                );
            }

            int timeoutInSeconds = route.TimeoutValue ?? 100;

            using CancellationTokenSource cancellationTokenSource = new(
                TimeSpan.FromSeconds(timeoutInSeconds)
            );
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            HttpClient httpClient = _httpClientFactory.CreateClient();
            var downstreamResponse = await httpClient.SendAsync(
                downstreamRequest,
                cancellationToken
            );

            DecrementActiveConnections(leastConnectionHost.Host, leastConnectionHost.Port);

            return downstreamResponse;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<HttpResponseMessage> ProcessLeastConnectionLoadBalancingRequestV2(
        HttpContext httpContext,
        Route route
    )
    {
        try
        {
            var requestPath = httpContext.Request.Path.ToString();
            var requestMethod = httpContext.Request.Method;

            Downstreamhostandport? leastConnectionHost = null;

            if (!string.IsNullOrEmpty(route.ServiceName))
            {
                leastConnectionHost = await GetLeastConnectionDownstreamHostFromServiceRegistryV1(route.ServiceName);
            }
            else if (route.DownstreamHostAndPorts is { Length: > 0 })
            {
                leastConnectionHost = GetLeastConnectionDownstreamHost(
                    route.DownstreamHostAndPorts
                );
            }

            if (leastConnectionHost is null)
                throw new Exception("No available downstream instances.");

            IncrementActiveConnections(leastConnectionHost.Host, leastConnectionHost.Port);

            string upstreamBasePath = route.UpstreamPathTemplate.Replace("{everything}", "");
            string downstreamBasePath = route.DownstreamPathTemplate.Replace("{everything}", "");

            string downstreamPath = requestPath.Replace(upstreamBasePath, downstreamBasePath);

            string downstreamUrl =
                $"{route.DownstreamScheme}://{leastConnectionHost.Host}:{leastConnectionHost.Port}{downstreamPath}";
            if (httpContext.Request.QueryString.HasValue)
            {
                downstreamUrl += httpContext.Request.QueryString;
            }

            var downstreamRequest = new HttpRequestMessage(
                new HttpMethod(requestMethod),
                downstreamUrl
            );

            if (httpContext.Request.Body.CanRead)
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();
                downstreamRequest.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json"
                );
            }

            int timeoutInSeconds = route.TimeoutValue ?? 100;

            using CancellationTokenSource cancellationTokenSource = new(
                TimeSpan.FromSeconds(timeoutInSeconds)
            );
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            HttpClient httpClient = _httpClientFactory.CreateClient();
            var downstreamResponse = await httpClient.SendAsync(
                downstreamRequest,
                cancellationToken
            );

            DecrementActiveConnections(leastConnectionHost.Host, leastConnectionHost.Port);

            return downstreamResponse;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private Downstreamhostandport GetLeastConnectionDownstreamHost(
        Downstreamhostandport[] downstreamHosts
    )
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

    private async Task<Downstreamhostandport?> GetLeastConnectionDownstreamHostFromServiceRegistry(
        string serviceName
    )
    {
        var instances = await _appDbContext
            .Tbl_ServiceRegistries.AsNoTracking()
            .Where(x => x.ServiceName == serviceName)
            .Select(x => new Downstreamhostandport { Host = x.Host, Port = x.Port })
            .ToListAsync();

        if (instances.Count <= 0)
            return null;

        foreach (var instance in instances)
        {
            var key = $"{instance.Host}:{instance.Port}";
            _activeConnections.TryAdd(key, 0);
        }

        return instances
            .OrderBy(instance => _activeConnections[$"{instance.Host}:{instance.Port}"])
            .FirstOrDefault();
    }

    private async Task<Downstreamhostandport?> GetLeastConnectionDownstreamHostFromServiceRegistryV1(
        string serviceName
    )
    {
        HttpClient serviceDiscoveryClient = _httpClientFactory.CreateClient("ServiceDiscoveryClient");
        HttpRequestMessage serviceDiscoveryRequest = new HttpRequestMessage()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(serviceDiscoveryClient.BaseAddress!, $"/api/ServiceDiscovery/DiscoverService?serviceName={serviceName}"),
        };
        HttpResponseMessage serviceDiscoveryResponse = await serviceDiscoveryClient.SendAsync(serviceDiscoveryRequest);
        serviceDiscoveryResponse.EnsureSuccessStatusCode();

        string responseJson = await serviceDiscoveryResponse.Content.ReadAsStringAsync();
        var model = JsonConvert.DeserializeObject<Result<List<ServiceDiscoveryModel>>>(responseJson) ?? throw new ArgumentException("No service found.");
        var instances = model!.Data.Select(x => new Downstreamhostandport
        {
            Host = x.HostName,
            Port = x.Port
        }).ToList();


        if (instances.Count <= 0)
            return null;

        foreach (var instance in instances)
        {
            var key = $"{instance.Host}:{instance.Port}";
            _activeConnections.TryAdd(key, 0);
        }

        return instances
            .OrderBy(instance => _activeConnections[$"{instance.Host}:{instance.Port}"])
            .FirstOrDefault();
    }
}
