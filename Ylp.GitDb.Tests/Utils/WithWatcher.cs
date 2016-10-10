using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Internal;
using FluentAssertions;
using LibGit2Sharp;
using Moq.AutoMock;
using Ylp.GitDb.Core;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Local;

namespace Ylp.GitDb.Tests.Utils
{
    public abstract class WithWatcher : IDisposable
    {
        protected readonly GitDb.Watcher.Watcher Subject;
        protected readonly string LocalPath = Path.GetTempPath() + Guid.NewGuid();
        protected readonly IGitDb GitDb;
        protected readonly Repository Repo;
        protected readonly Author Author = new Author("author", "author@mail.com");

        protected WithWatcher()
        {
            GitDb = new LocalGitDb(LocalPath, new AutoMocker().Get<ILogger>());
            Repo = new Repository(LocalPath);
            Setup().Wait();
            Subject = new GitDb.Watcher.Watcher(LocalPath, new AutoMocker().Get<ILogger>(), 5);
            Subject.MonitorEvents();
            Subject.Start();
            Because().Wait();
            Thread.Sleep(100);
        }

        protected virtual Task Setup() => Task.CompletedTask;

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
            Repo.Dispose();
            Subject.Dispose();
            GitDb.Dispose();
            deleteReadOnlyDirectory(LocalPath);
        }
    }
}