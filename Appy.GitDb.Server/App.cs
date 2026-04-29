using System.Collections.Generic;
using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Server.Auth;
using Appy.GitDb.Server.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NLog.LayoutRenderers;
using AppyExceptionLayoutRenderer = Appy.GitDb.Server.Logging.ExceptionLayoutRenderer;

namespace Appy.GitDb.Server
{
    public class App
    {
        App() { }

        static App()
        {
            LayoutRenderer.Register<AppyExceptionLayoutRenderer>("appy-exception");
            LayoutRenderer.Register<CorrelationIdLayoutRenderer>("correlationid");
        }

        public static WebApplication Create(string url, IGitDb repo, IEnumerable<User> users)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls(url);
            ConfigureServices(builder.Services, repo, users);
            var app = builder.Build();
            ConfigurePipeline(app);
            return app;
        }

        public static IWebHostBuilder CreateTestBuilder(IGitDb repo, IEnumerable<User> users) =>
            new WebHostBuilder()
                .ConfigureServices(services => ConfigureServices(services, repo, users))
                .Configure(ConfigurePipeline);

        static void ConfigureServices(IServiceCollection services, IGitDb repo, IEnumerable<User> users)
        {
            services.AddSingleton(repo);
            services.AddSingleton(users);
            services.AddTransient<LoggingMiddleware>();
            services.AddAuthentication("Basic")
                .AddScheme<BasicAuthOptions, BasicAuthHandler>("Basic", null);
            services.AddAuthorization();
            services.AddResponseCompression();
            services.AddControllers()
                .AddNewtonsoftJson();
        }

        static void ConfigurePipeline(IApplicationBuilder app)
        {
            app.UseMiddleware<LoggingMiddleware>();
            app.UseResponseCompression();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}
