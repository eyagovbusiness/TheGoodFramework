using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheGoodFramework.CA.Presentation.Middleware;
using TheGoodFramework.Common.Logging;

namespace TheGoodFramework.CA.Application
{
    /// <summary>
    /// Class to support logic abstraction on the WebApplication creation.
    /// </summary>
    public static class WebApplicationAbstraction
    {
        /// <summary>
        /// Creates a new instance of <see cref="WebApplication"/> with custom serilog configuration from <see cref="LoggerConfigurator"/>. Also additional <see cref="WebApplicationBuilder"/> actions can included in the build.
        /// </summary>
        /// <param name="aWebHostBuilderAction">Custom logic to add on the WebApplicationBuilder that will be used to build the resulting <see cref="WebApplication"/>.</param>
        /// <returns>A new customized instance of <see cref="WebApplication"/>.</returns>
        public static WebApplication CreateCustomWebApplication(Action<WebApplicationBuilder>? aWebHostBuilderAction = null)
        {
            WebApplicationBuilder lBuilder = WebApplication.CreateBuilder();
            lBuilder.Host.ConfigureSerilog();
            lBuilder.Services.AddControllers();
            lBuilder.Services.AddEndpointsApiExplorer();
            lBuilder.Services.AddSwaggerGen();

            if (aWebHostBuilderAction != null)
                aWebHostBuilderAction.Invoke(lBuilder);

            return lBuilder.Build();

        }

        public static void CustomRun(this WebApplication aWebApplication)
        {

            if (aWebApplication.Environment.IsDevelopment())
            {
                aWebApplication.UseSwagger();
                aWebApplication.UseSwaggerUI();
            }
            else
                aWebApplication.UseHttpsRedirection();

            aWebApplication.UseMiddleware<LoggingMiddleware>();
            aWebApplication.UseRouting();
            aWebApplication.UseAuthorization();
            aWebApplication.MapControllers();
            aWebApplication.Run();
        }

    }
}