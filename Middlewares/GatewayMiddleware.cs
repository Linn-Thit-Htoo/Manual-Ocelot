namespace Manual_Ocelot.Middlewares;

public class GatewayMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Ocelot _ocelot;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<GatewayMiddleware> _logger;

    public GatewayMiddleware(
        IServiceScopeFactory serviceScopeFactory,
        RequestDelegate next,
        IWebHostEnvironment webHostEnvironment,
        ILogger<GatewayMiddleware> logger
    )
    {
        string filePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            $"ocelot.{webHostEnvironment!.EnvironmentName}.json"
        );

        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(Directory.GetCurrentDirectory(), "ocelot.json");
        }

        _serviceScopeFactory = serviceScopeFactory;

        string jsonStr = File.ReadAllText(filePath);
        _ocelot = JsonConvert.DeserializeObject<Ocelot>(jsonStr)!;
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        try
        {
            var requestPath = httpContext.Request.Path.ToString();
            var requestMethod = httpContext.Request.Method;
            var ip = httpContext.Connection.RemoteIpAddress!.ToString();
            var scope = _serviceScopeFactory.CreateScope();

            var route = _ocelot.Routes.FirstOrDefault(r =>
                requestPath.StartsWith(
                    r.UpstreamPathTemplate.Replace("{everything}", ""),
                    StringComparison.OrdinalIgnoreCase
                ) && r.UpstreamHttpMethod.Contains(requestMethod)
            );

            if (route is null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                await httpContext.Response.WriteAsync("Downstream service not found.");
                return;
            }

            if (
                route.AuthenticationOptions is not null
                && route.AuthenticationOptions.AuthenticationProviderKey is not null
            )
            {
                #region Check Auth

                string authHeader = httpContext.Request.Headers["Authorization"]!;

                if (authHeader is null)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                string[] header_and_token = authHeader.Split(' ');
                string header = header_and_token[0];
                string token = header_and_token[1];

                if (!header.StartsWith("Bearer"))
                {
                    httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                #endregion

                #region Allowed Scopes

                if (
                    route.AuthenticationOptions.AllowedScopes is not null
                    && route.AuthenticationOptions.AllowedScopes.Length > 0
                )
                {
                    string key =
                        _ocelot.GlobalConfiguration.JwtKey!
                        ?? throw new Exception("Jwt Key Not Found.");
                    byte[] jwtKey = Encoding.ASCII.GetBytes(key);

                    var tokenValidationService =
                        scope.ServiceProvider.GetRequiredService<ITokenValidationService>();

                    var principal = tokenValidationService.ValidateToken(jwtKey, token);
                    if (principal is null)
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    }

                    bool hasValidScope = route.AuthenticationOptions.AllowedScopes.All(scope =>
                        principal.Claims.Any(c => c.Type == "scope" && c.Value == scope)
                    );

                    if (!hasValidScope)
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }
                }

                #endregion
            }

            #region rate limit

            if (route.RateLimitOptions is not null && route.RateLimitOptions.EnableRateLimiting)
            {
                List<string> whiteListIps = route.RateLimitOptions.ClientWhitelist.ToList();
                if (!whiteListIps.Any(x => x.Equals(ip)))
                {
                    var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
                    string cacheKey = $"{ip}:{route.UpstreamPathTemplate}";

                    if (cache.TryGetValue(cacheKey, out int requestCount))
                    {
                        if (requestCount >= route.RateLimitOptions.Limit)
                        {
                            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                            return;
                        }
                    }

                    cache.Set(
                        cacheKey,
                        requestCount + 1,
                        TimeSpan.FromSeconds(route.RateLimitOptions.PeriodTimespan)
                    );
                }
            }

            #endregion

            var gatewayService = scope.ServiceProvider.GetRequiredService<IGatewayService>();

            HttpResponseMessage response = null!;

            #region Load Balancing

            response = route.LoadBalancerOptions.Type switch
            {
                nameof(LoadBalancingConstant.RoundRobin) =>
                    await gatewayService.ProcessRoundRobinLoadBalancingRequestV1(
                        httpContext,
                        route
                    ),
                nameof(LoadBalancingConstant.LeastConnection) =>
                    await gatewayService.ProcesssLeastConnectionLoadBalancingRequestV1(
                        httpContext,
                        route
                    ),
                _ => throw new Exception("Invalid Load Balancing Algorithm."),
            };

            #endregion

            httpContext.Response.StatusCode = (int)response!.StatusCode;
            string jsonStr = await response.Content.ReadAsStringAsync();
            await httpContext.Response.WriteAsync(jsonStr);

            return;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Downstream error: {ex.ToString()}");
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadGateway;
        }
    }
}
