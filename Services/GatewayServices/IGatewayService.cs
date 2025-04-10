namespace Manual_Ocelot.Services.GatewayServices;

public interface IGatewayService
{
    Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequest(
        HttpContext httpContext,
        Route route
    );
    //Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequestV1(
    //    HttpContext httpContext,
    //    Route route
    //);
    Task<HttpResponseMessage> ProcessRoundRobinLoadBalancingRequestV2(
        HttpContext httpContext,
        Route route
    );
    Task<HttpResponseMessage> ProcessLeastConnectionLoadBalancingRequest(
        HttpContext httpContext,
        Route route
    );
    //Task<HttpResponseMessage> ProcessLeastConnectionLoadBalancingRequestV1(
    //    HttpContext httpContext,
    //    Route route
    //);
    Task<HttpResponseMessage> ProcessLeastConnectionLoadBalancingRequestV2(
        HttpContext httpContext,
        Route route
    );
}
