using System;
using System.Collections.Generic;
using System.Web.Http;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.Owin.Hosting;
using Owin;
using Thinktecture.IdentityModel.Owin;
using Ylp.GitDb.Core;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Server.Auth;

namespace Ylp.GitDb.Server
{
    public class App
    {
        App() { }
        IContainer _container;
        string _url;
        IEnumerable<User> _users;
        public static App Create(string url, IGitDb repo, ILogger serverLog, IEnumerable<User> users)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(repo).As<IGitDb>().ExternallyOwned();
            builder.RegisterApiControllers(typeof(App).Assembly);

            var app = new App
            {
                _container = builder.Build(),
                _url = url,
                _users = users
            };
            LoggingMiddleware.Logger = serverLog;
            return app;
        }

        public IDisposable Start() => 
            WebApp.Start(new StartOptions(_url), Configuration);

        public void Configuration(IAppBuilder app)
        {
            var config = new HttpConfiguration();
            app.UseAutofacMiddleware(_container);
            var auth = new Authentication(_users);
            app.UseBasicAuthentication("ylp.gitdb", auth.ValidateUsernameAndPassword);
            config.MapHttpAttributeRoutes();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(_container);
            app.Use<LoggingMiddleware>();
            app.UseWebApi(config);
        }
    }
}