namespace Manual_Ocelot.Services.GatewayServices;

public interface IGatewayService
{
    Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequest(
        HttpContext httpContext,
        Route route
    );
    Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequestV1(
        HttpContext httpContext,
        Route route
    );
    Task<HttpResponseMessage> ProcesssLeastConnectionLoadBalancingRequest(
        HttpContext httpContext,
        Route route
    );
    Task<HttpResponseMessage> ProcesssLeastConnectionLoadBalancingRequestV1(
        HttpContext httpContext,
        Route route
    );
}
