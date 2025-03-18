using Route = Manual_Ocelot.Configurations.Route;

namespace Manual_Ocelot.Services.GatewayServices;

public interface IGatewayService
{
    Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequest(
        HttpContext httpContext,
        Route route
    );
    Task<HttpResponseMessage> ProcesssLeastConnectionLoadBalancingRequest(
        HttpContext httpContext,
        Route route
    );
}
