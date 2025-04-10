namespace Manual_Ocelot.Dependencies;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddOcelot(
        this IServiceCollection services,
        WebApplicationBuilder builder
    )
    {
        builder
            .Configuration.SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile(
                $"appsettings.{builder.Environment.EnvironmentName}.json",
                optional: false,
                reloadOnChange: true
            )
            .AddEnvironmentVariables();

        builder
            .Services.AddControllers()
            .AddJsonOptions(opt =>
            {
                opt.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

        builder
            .Services
            .AddSingleton<IGatewayService, GatewayService>()
            .AddScoped<ITokenValidationService, TokenValidationService>()
            .AddHttpClient()
            .AddMemoryCache()
            .Configure<AppSetting>(builder.Configuration)
            .AddEndpointsApiExplorer()
            .AddSwaggerGen();

        builder.Services.AddHttpClient("ServiceDiscoveryClient", opt =>
        {
            opt.BaseAddress = new Uri("http://123.253.20.225:81");
        });

        return services;
    }

    public static IApplicationBuilder UseOcelot(this WebApplication app)
    {
        return app.UseMiddleware<GatewayMiddleware>();
    }
}
