using System;
using System.Collections.Generic;
using System.Configuration;
using Appy.GitDb.Local;
using Appy.GitDb.Server.Auth;
using Appy.GitDb.Server.Logging;
using NLog.LayoutRenderers;
using ExceptionLayoutRenderer = Appy.GitDb.Server.Logging.ExceptionLayoutRenderer;

namespace Appy.GitDb.Server
{
    class Program
    {
        public static void Main(string[] args)
        {
            LayoutRenderer.Register<ExceptionLayoutRenderer>("appy-exception");
            LayoutRenderer.Register<CorrelationIdLayoutRenderer>("correlationid");
            var url = ConfigurationManager.AppSettings["server.url"];
            var gitRepoPath = ConfigurationManager.AppSettings["git.repository.basePath"];
            var remoteUrl = ConfigurationManager.AppSettings["remote.url"];
            var userName = ConfigurationManager.AppSettings["remote.user.name"];
            var userEmail = ConfigurationManager.AppSettings["remote.user.email"];
            var password = ConfigurationManager.AppSettings["remote.user.password"];
            
            var app = App.Create(url, new LocalGitServer(gitRepoPath, remoteUrl, userName, userEmail, password), new List<User>
            {
                new User{ UserName = "GitAdmin", Password = ConfigurationManager.AppSettings["GitAdmin"], Roles = new [] { "admin","read","write" }},
                new User{ UserName = "GitReader",Password = ConfigurationManager.AppSettings["GitAdmin"],Roles = new [] { "read" }},
                new User{ UserName = "GitWriter", Password = ConfigurationManager.AppSettings["GitWriter"] ,Roles = new [] { "write" }}
            });
            using (app.Start())
            {
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
        }
    }
}
