using System;
using System.Collections.Generic;
using System.Configuration;
using NLog.LayoutRenderers;
using Ylp.GitDb.Local;
using Ylp.GitDb.Server.Auth;
using Ylp.GitDb.Server.Logging;
using ExceptionLayoutRenderer = Ylp.GitDb.Server.Logging.ExceptionLayoutRenderer;

namespace Ylp.GitDb.Server
{
    class Program
    {
        public static void Main(string[] args)
        {
            LayoutRenderer.Register<ExceptionLayoutRenderer>("ylp-exception");
            LayoutRenderer.Register<CorrelationIdLayoutRenderer>("correlationid");
            var url = ConfigurationManager.AppSettings["server.url"];
            var gitRepoPath = ConfigurationManager.AppSettings["git.repository.path"];
            var remoteUrl = ConfigurationManager.AppSettings["remote.url"];
            var userName = ConfigurationManager.AppSettings["remote.user.name"];
            var userEmail = ConfigurationManager.AppSettings["remote.user.email"];
            var password = ConfigurationManager.AppSettings["remote.user.password"];
            if (!int.TryParse(ConfigurationManager.AppSettings["transactions.timeout"], out int transactionTimeout))
                transactionTimeout = 10;

            var app = App.Create(url, new LocalGitDb(gitRepoPath, remoteUrl, userName, userEmail, password, transactionTimeout), new List<User>
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
