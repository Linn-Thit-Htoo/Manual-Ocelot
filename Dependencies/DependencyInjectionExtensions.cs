﻿namespace Manual_Ocelot.Dependencies;

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
            .Services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseNpgsql(builder.Configuration.GetConnectionString("DbConnection"));
            })
            .AddSingleton<IGatewayService, GatewayService>()
            .AddScoped<ITokenValidationService, TokenValidationService>()
            .AddHttpClient()
            .AddMemoryCache()
            .Configure<AppSetting>(builder.Configuration)
            .AddEndpointsApiExplorer()
            .AddSwaggerGen();

        return services;
    }

    public static IApplicationBuilder UseOcelot(this WebApplication app)
    {
        return app.UseMiddleware<GatewayMiddleware>();
    }
}
