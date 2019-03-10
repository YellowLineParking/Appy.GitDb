using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Local;
using Appy.GitDb.NetCore.Server.Auth;
using Appy.GitDb.NetCore.Server.Logging;
using Appy.GitDb.NetCore.Server.Settings;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;

namespace Appy.GitDb.NetCore.Server
{
    public class Startup
    {
        const string BasicAuthScheme = "BasicAuthentication";
        public IConfiguration Configuration { get; }
        public IContainer Container { get; private set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            LoggingMiddleware.Logger = LogManager.GetLogger("server-log");
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var configuration = services.BuildServiceProvider()
                .GetService<IConfiguration>();

            var gitDbSettings = new GitDbSettings();
            configuration.Bind("GitDb", gitDbSettings);

            services
                .AddOptions()
                .Configure<GitDbSettings>(settings => configuration.Bind("GitDb", settings))
                .Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest)
                .AddResponseCompression(options => { options.Providers.Add<GzipCompressionProvider>(); }); // options.EnableForHttps = true;
            
            services                
                .AddAuthentication(config =>
                {
                    config.DefaultAuthenticateScheme = BasicAuthScheme;
                    config.DefaultChallengeScheme = BasicAuthScheme;
                    config.DefaultScheme = BasicAuthScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthScheme, null);

            services
                .AddMvc(o => o.Filters.Add<HttpGlobalExceptionFilter>())
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            var repo = new LocalGitDb(
                gitDbSettings.GitHomePath,
                gitDbSettings.Remote.Url, gitDbSettings.Remote.UserName, gitDbSettings.Remote.Email, gitDbSettings.Remote.Password,
                gitDbSettings.TransactionsTimeout);

            var builder = new ContainerBuilder();
            builder.Populate(services);
            builder.RegisterInstance(repo).As<IGitDb>().ExternallyOwned();
            builder.RegisterType<UserService>().As<IUserService>().SingleInstance();
            Container = builder.Build();

            return new AutofacServiceProvider(Container);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment() || env.EnvironmentName == "local")
            {
                app.UseDeveloperExceptionPage();
            }

            app
               .UseResponseCompression()
               .UseMiddleware<LoggingMiddleware>()
               .UseAuthentication()
               .UseMvc();
        }
    }
}