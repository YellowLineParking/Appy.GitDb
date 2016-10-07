using System;
using System.Web.Http;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.Owin.Hosting;
using Owin;
using Ylp.GitDb.Core;
using Ylp.GitDb.Core.Interfaces;

namespace Ylp.GitDb.Server
{
    public class App
    {
        App() { }
        IContainer _container;
        string _url;
        public static App Create(string url, IGitDb repo, ILogger serverLog)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(repo).As<IGitDb>().ExternallyOwned();
            builder.RegisterApiControllers(typeof(App).Assembly);
            var app = new App
            {
                _container = builder.Build(),
                _url = url
            };
            
            
            
            LoggingMiddleware.Logger = serverLog;
            return app;
        }

        public IDisposable Start() =>
            WebApp.Start<App>(_url);

        public void Configuration(IAppBuilder app)
        {
            var config = new HttpConfiguration();
            app.UseAutofacMiddleware(_container);
            config.MapHttpAttributeRoutes();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(_container);
            app.Use<LoggingMiddleware>();
            app.UseWebApi(config);
        }
    }
}