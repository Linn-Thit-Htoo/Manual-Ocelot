using System.Net;
using Manual_Ocelot.Configurations;
using Manual_Ocelot.Constants;
using Manual_Ocelot.Services.GatewayServices;
using Manual_Ocelot.Services.TokenValidationServices;
using Newtonsoft.Json;

namespace Manual_Ocelot.Middlewares;

public class GatewayMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Ocelot _ocelot;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public GatewayMiddleware(
        IServiceScopeFactory serviceScopeFactory,
        RequestDelegate next,
        IWebHostEnvironment webHostEnvironment
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
    }

    public async Task Invoke(HttpContext httpContext)
    {
        try
        {
            var requestPath = httpContext.Request.Path.ToString();
            var requestMethod = httpContext.Request.Method;
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
                    var tokenValidationService =
                        scope.ServiceProvider.GetRequiredService<ITokenValidationService>();

                    var principal = tokenValidationService.ValidateToken(token);
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

            var gatewayService = scope.ServiceProvider.GetRequiredService<IGatewayService>();

            HttpResponseMessage response = null!;

            response = route.LoadBalancerOptions.Type switch
            {
                nameof(LoadBalancingConstant.RoundRobin) =>
                    await gatewayService.ProcessRoundRobinLoadBalancingRequest(httpContext, route),
                nameof(LoadBalancingConstant.LeastConnection) =>
                    await gatewayService.ProcesssLeastConnectionLoadBalancingRequest(
                        httpContext,
                        route
                    ),
                _ => throw new Exception("Invalid Load Balancing Algorithm."),
            };

            httpContext.Response.StatusCode = (int)response!.StatusCode;
            string jsonStr = await response.Content.ReadAsStringAsync();
            await httpContext.Response.WriteAsync(jsonStr);
            return;
        }
        catch (Exception ex)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadGateway;
        }
    }
}
