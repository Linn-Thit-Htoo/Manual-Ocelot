using Manual_Ocelot.Configurations;
using Manual_Ocelot.Dependencies;
using Manual_Ocelot.Middlewares;
using Manual_Ocelot.Services.GatewayServices;
using Manual_Ocelot.Services.TokenValidationServices;

namespace Manual_Ocelot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddDependencies(builder);

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseMiddleware<GatewayMiddleware>();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
