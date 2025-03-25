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

        builder.Services.AddDbContext<AppDbContext>(opt =>
        {
            opt.UseNpgsql(builder.Configuration.GetConnectionString("DbConnection"));
        });

        builder.Services.AddSingleton<IGatewayService, GatewayService>();
        builder.Services.AddScoped<ITokenValidationService, TokenValidationService>();
        builder.Services.Configure<AppSetting>(builder.Configuration);
        builder.Services.AddHttpClient();

        return services;
    }

    public static IApplicationBuilder UseOcelot(this WebApplication app)
    {
        return app.UseMiddleware<GatewayMiddleware>();
    }
}
