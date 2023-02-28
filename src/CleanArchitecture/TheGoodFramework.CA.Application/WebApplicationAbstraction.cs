using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TGF.CA.Presentation.Middleware;
using TGF.Common.Logging;

namespace TGF.CA.Application
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

        /// <summary>
        /// Async version of <see cref="CreateCustomWebApplication"/>, the usage of this version is very specific and it is not recommended for general purpose.
        /// </summary>
        /// <param name="aWebHostBuilderFunction">Custom logic to add on the WebApplicationBuilder that will be used to build the resulting <see cref="WebApplication"/>.</param>
        /// <returns>A new customized instance of <see cref="WebApplication"/>.</returns>
        /// <remarks>Any service added from aWebHostBuilderAction function will not be possible to be injected any dependency from the host DI container if the dependency is injected via async creation method instead of the synchronous class constructor of the service.</remarks>
        public static async Task<WebApplication> CreateCustomWebApplicationAsync(Func<WebApplicationBuilder, Task>? aWebHostBuilderFunction = null)
        {
            WebApplicationBuilder lBuilder = WebApplication.CreateBuilder();
            lBuilder.Host.ConfigureSerilog();
            lBuilder.Services.AddControllers();
            lBuilder.Services.AddEndpointsApiExplorer();
            lBuilder.Services.AddSwaggerGen();

            if (aWebHostBuilderFunction != null)
                await aWebHostBuilderFunction.Invoke(lBuilder);


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