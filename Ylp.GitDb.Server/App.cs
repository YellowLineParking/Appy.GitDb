﻿using System;
using System.Collections.Generic;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Hosting;
using NLog;
using NLog.Config;
using Owin;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Server.Auth;
using Ylp.GitDb.Server.Logging;
using ExceptionLogger = Ylp.GitDb.Server.Logging.ExceptionLogger;

namespace Ylp.GitDb.Server
{
    public class App
    {
        App(){}
        IContainer _container;
        string _url;
        IEnumerable<User> _users;
        Logger _serverLog;

        public static App Create(string url, IGitDb repo, IEnumerable<User> users)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(repo).As<IGitDb>().ExternallyOwned();
            builder.RegisterApiControllers(typeof(App).Assembly);

            var app = new App
            {
                _container = builder.Build(),
                _url = url,
                _users = users,
                _serverLog = LogManager.GetLogger("server-log")
            };
            LoggingMiddleware.Logger = LogManager.GetLogger("server-log");
            return app;
        }

        public IDisposable Start()
        {
            try
            {
                _serverLog.Info("Starting up git server");
                var result = WebApp.Start(new StartOptions(_url), Configuration);
                _serverLog.Info($"Server started on {_url}");
                return result;
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
            config.Services.Add(typeof(IExceptionLogger), new ExceptionLogger());
            app.UseAutofacMiddleware(_container);
            var auth = new Authentication(_users);
            app.UseBasicAuthentication("ylp.gitdb", auth.ValidateUsernameAndPassword);
            config.MapHttpAttributeRoutes();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(_container);
            
            app.Use<LoggingMiddleware>();
            app.UseCompressionModule(OwinCompression.DefaultCompressionSettings);
            app.UseStageMarker(PipelineStage.MapHandler);
            app.UseWebApi(config);
        }
    }
}