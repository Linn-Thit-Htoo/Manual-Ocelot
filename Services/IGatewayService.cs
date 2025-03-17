using Route = Manual_Ocelot.Configurations.Route;

namespace Manual_Ocelot.Services
{
    public interface IGatewayService
    {
        Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequest(HttpContext httpContext, Route route);
    }
}
