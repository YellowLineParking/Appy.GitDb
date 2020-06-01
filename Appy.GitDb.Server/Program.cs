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
            var gitRepoPath = ConfigurationManager.AppSettings["git.repository.path"];
            var remoteName = ConfigurationManager.AppSettings["remote.name"];
            var remoteUrl = ConfigurationManager.AppSettings["remote.url"];
            var userName = ConfigurationManager.AppSettings["remote.user.name"];
            var userEmail = ConfigurationManager.AppSettings["remote.user.email"];
            var password = ConfigurationManager.AppSettings["remote.user.password"];
            var remoteBranchSync = ConfigurationManager.AppSettings["remote.branch.sync"];
            if (!int.TryParse(ConfigurationManager.AppSettings["transactions.timeout"], out int transactionTimeout))
                transactionTimeout = 10;

            var app = App.Create(url, new LocalGitDb(gitRepoPath, remoteName, remoteUrl, userName, userEmail, password, remoteBranchSync, transactionTimeout), new List<User>
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
