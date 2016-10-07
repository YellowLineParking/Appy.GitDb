using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Castle.Core.Internal;
using LibGit2Sharp;
using Microsoft.Owin.Testing;
using Moq.AutoMock;
using Ylp.GitDb.Core;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Local;
using Ylp.GitDb.Remote;
using Ylp.GitDb.Server;

namespace Ylp.GitDb.Tests.Utils
{
    public abstract class WithRepo : IDisposable
    {
        protected readonly IGitDb Subject;
        protected readonly string LocalPath = Path.GetTempPath() + Guid.NewGuid();
        protected readonly Repository Repo;
        protected readonly Author Author = new Author("author", "author@mail.com");
        readonly TestServer _server;

        protected WithRepo()
        {
            const string url = "http://localhost"; // this is a dummy url, requests are in-memory, not over the network
            var app = App.Create(url, new LocalGitDb(LocalPath, new AutoMocker().Get<ILogger>()), new AutoMocker().Get<ILogger>());
            _server = TestServer.Create(app.Configuration);

            Subject = new RemoteGitDb(_server.HttpClient, url);
            Repo = new Repository(LocalPath);
            Because().Wait();
        }

        protected virtual Task Because() => Task.CompletedTask;

        static void deleteReadOnlyDirectory(string directory)
        {
            Directory.EnumerateDirectories(directory)
                .ForEach(deleteReadOnlyDirectory);
            Directory.EnumerateFiles(directory).Select(file => new FileInfo(file) { Attributes = FileAttributes.Normal })
                .ForEach(fi => fi.Delete());

            Directory.Delete(directory);
        }
        public void Dispose()
        {
            _server.Dispose();
            Subject.Dispose();
            Repo.Dispose();
            deleteReadOnlyDirectory(LocalPath);
        }
    }
}