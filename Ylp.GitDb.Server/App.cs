using System;
using System.Collections.Generic;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Hosting;
using Owin;
using Ylp.GitDb.Core;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Server.Auth;

namespace Ylp.GitDb.Server
{
    public class App
    {
        App(){}
        IContainer _container;
        string _url;
        IEnumerable<User> _users;
        ILogger _serverLog;

        public static App Create(string url, IGitDb repo, ILogger serverLog, IEnumerable<User> users)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(repo).As<IGitDb>().ExternallyOwned();
            builder.RegisterApiControllers(typeof(App).Assembly);

            var app = new App
            {
                _container = builder.Build(),
                _url = url,
                _users = users,
                _serverLog = serverLog
            };
            LoggingMiddleware.Logger = serverLog;
            return app;
        }

        public IDisposable Start()
        {
            try
            {
                _serverLog.Info("Starting up git server");
                return WebApp.Start(new StartOptions(_url), Configuration);
            }
            catch (Exception ex)
            {
                _serverLog.Fatal(ex, string.Empty);
                throw;
            }
        }
            

        public void Configuration(IAppBuilder app)
        {
            var config = new HttpConfiguration();
            config.Services.Add(typeof(IExceptionLogger), new ExceptionLogger(_serverLog));
            app.UseAutofacMiddleware(_container);
            var auth = new Authentication(_users);
            app.UseBasicAuthentication("ylp.gitdb", auth.ValidateUsernameAndPassword);
            config.MapHttpAttributeRoutes();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(_container);
            
            app.Use<LoggingMiddleware>();
            app.UseStageMarker(PipelineStage.MapHandler);
            app.UseWebApi(config);
        }
    }
}