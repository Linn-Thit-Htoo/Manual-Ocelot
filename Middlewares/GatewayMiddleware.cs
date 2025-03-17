using Manual_Ocelot.Configurations;
using Manual_Ocelot.Constants;
using Manual_Ocelot.Services;
using Newtonsoft.Json;
using System.Net;
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

                HttpResponseMessage response = null!;

                switch (route.LoadBalancerOptions.Type)
                {
                    case nameof(LoadBalancingConstant.RoundRobin):
                        response = await gateway.ProcessRoundRobinLoadBalancingRequest(httpContext, route);
                        break;
                    case nameof(LoadBalancingConstant.LeastConnection):
                        response = await gateway.ProcesssLeastConnectionLoadBalancingRequest(httpContext, route);
                        break;
                }

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
}
