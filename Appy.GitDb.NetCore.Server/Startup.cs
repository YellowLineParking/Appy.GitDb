using Appy.GitDb.NetCore.Server.Auth;
using Appy.GitDb.NetCore.Server.Compression;
using Appy.GitDb.NetCore.Server.GitDb;
using Appy.GitDb.NetCore.Server.Logging;
using Appy.GitDb.NetCore.Server.Mvc;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;

namespace Appy.GitDb.NetCore.Server
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IContainer Container { get; private set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            AppyLoggingMiddleware.Logger = LogManager.GetLogger("server-log");
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services
                .AddOptions()
                .AddGzipCompression()
                .AddBasicAuthentication()
                .AddCustomMvc();

            var builder = new ContainerBuilder();
            builder.Populate(services);
            builder.AddGitDb(services);

            Container = builder.Build();
            return new AutofacServiceProvider(Container);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment() || env.EnvironmentName == "local")
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseResponseCompression()
                .UseAppyLogging()
                .UseAuthentication()
                .UseMvc();
        }
    }
}