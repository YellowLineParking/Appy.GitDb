using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Internal;
using FluentAssertions;
using LibGit2Sharp;
using Moq.AutoMock;
using Xunit;
using Ylp.GitDb.Core;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Local;
using Ylp.GitDb.Watcher;

namespace Ylp.GitDb.Tests.Utils
{
    public abstract class WithWatcher : IAsyncLifetime
    {
        protected GitDb.Watcher.Watcher Subject;
        protected readonly string LocalPath = Path.GetTempPath() + Guid.NewGuid();
        protected IGitDb GitDb;
        protected Repository Repo;
        protected readonly Author Author = new Author("author", "author@mail.com");

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

        public async Task InitializeAsync()
        {
            GitDb = new LocalGitDb(LocalPath, new AutoMocker().Get<ILogger>());
            Repo = new Repository(LocalPath);
            await Setup();
            Subject = new GitDb.Watcher.Watcher(LocalPath, new AutoMocker().Get<ILogger>(), 1);
            Subject.MonitorEvents();
            Subject.Start(new List<BranchInfo>());
            await Because();
            Thread.Sleep(150);
        }

        public Task DisposeAsync()
        {
            Repo.Dispose();
            Subject.Dispose();
            GitDb.Dispose();
            deleteReadOnlyDirectory(LocalPath);
            return Task.CompletedTask;
        }
    }
}