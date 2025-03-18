using Manual_Ocelot.Configurations;
using Manual_Ocelot.Services.GatewayServices;
using Manual_Ocelot.Services.TokenValidationServices;

namespace Manual_Ocelot.Dependencies;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddDependencies(
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

        builder.Services.AddControllers();
        builder.Services.AddSingleton<IGatewayService, GatewayService>();
        builder.Services.AddScoped<ITokenValidationService, TokenValidationService>();
        builder.Services.Configure<AppSetting>(builder.Configuration);
        builder.Services.AddHttpClient();

        return services;
    }
}
