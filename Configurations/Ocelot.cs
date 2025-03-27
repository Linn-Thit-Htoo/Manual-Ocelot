namespace Manual_Ocelot.Configurations;

public class Ocelot
{
    public Globalconfiguration GlobalConfiguration { get; set; }
    public Route[] Routes { get; set; }
}

public class Globalconfiguration
{
    public string BaseUrl { get; set; }
    public string JwtKey { get; set; }
}

public class Route
{
    public string UpstreamPathTemplate { get; set; }
    public string[] UpstreamHttpMethod { get; set; }
    public Downstreamhostandport[] DownstreamHostAndPorts { get; set; }
    public Loadbalanceroptions LoadBalancerOptions { get; set; }
    public string DownstreamPathTemplate { get; set; }
    public string DownstreamScheme { get; set; }
    public Ratelimitoptions RateLimitOptions { get; set; }
    public string ServiceName { get; set; }
    public Authenticationoptions AuthenticationOptions { get; set; }
}

public class Loadbalanceroptions
{
    public string Type { get; set; }
}

public class Ratelimitoptions
{
    public string[] ClientWhitelist { get; set; }
    public bool EnableRateLimiting { get; set; }
    public int PeriodTimespan { get; set; }
    public int Limit { get; set; }
}

public class Authenticationoptions
{
    public string AuthenticationProviderKey { get; set; }
    public string[] AllowedScopes { get; set; }
}

public class Downstreamhostandport
{
    public string Host { get; set; }
    public int Port { get; set; }
}
