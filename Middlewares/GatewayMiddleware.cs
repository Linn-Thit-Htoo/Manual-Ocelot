using Manual_Ocelot.Configurations;
using Manual_Ocelot.Constants;
using Manual_Ocelot.Services;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

namespace Manual_Ocelot.Middlewares
{
    public class GatewayMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Ocelot _ocelot;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public GatewayMiddleware(IServiceScopeFactory serviceScopeFactory, RequestDelegate next)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "ocelot.json") ?? throw new Exception("Ocelot JSON file not found.");
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

                var route = _ocelot.Routes.FirstOrDefault(r =>
                requestPath.StartsWith(r.UpstreamPathTemplate.Replace("{everything}", ""), StringComparison.OrdinalIgnoreCase) &&
                r.UpstreamHttpMethod.Contains(requestMethod));

                if (route is null)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    await httpContext.Response.WriteAsync("Downstream service not found.");
                    return;
                }

                var scope = _serviceScopeFactory.CreateScope();
                var gateway = scope.ServiceProvider.GetRequiredService<IGatewayService>();

                if (route.LoadBalancerOptions.Type.Equals(LoadBalancingConstant.RoundRobin))
                {
                    var downstreamResponse = await gateway.ProcessRoundRobinLoadBalancingRequest(httpContext, route);

                    httpContext.Response.StatusCode = (int)downstreamResponse.StatusCode;
                    string response = await downstreamResponse.Content.ReadAsStringAsync();
                    await httpContext.Response.WriteAsync(response);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
