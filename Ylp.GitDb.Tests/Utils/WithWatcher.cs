using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LibGit2Sharp;
using Xunit;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Local;
using Ylp.GitDb.Watcher;

namespace Ylp.GitDb.Tests.Utils
{
    public abstract class WithWatcher : IAsyncLifetime
    {
        protected GitDb.Watcher.Watcher Subject;
        readonly string _localPath = Path.GetTempPath() + Guid.NewGuid();
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

        protected List<BranchAdded> BranchAdded = new List<BranchAdded>();
        protected List<BranchRemoved> BranchRemoved = new List<BranchRemoved>();
        protected List<BranchChanged> BranchChanged = new List<BranchChanged>();

        public async Task InitializeAsync()
        {
            GitDb = new LocalGitDb(_localPath);
            Repo = new Repository(_localPath);
            await Task.WhenAll(Enumerable.Range(0, 20)
                                         .Select(i => GitDb.Save("master", $"Commit {i}", new Document{Key = $"{i}.json", Value = i.ToString()},Author )));
            await Setup();
            Subject = new GitDb.Watcher.Watcher(_localPath, 1, addToList(BranchAdded), addToList(BranchChanged), addToList(BranchRemoved));
            
            await Subject.Start(new List<BranchInfo>());
            await Because();
            Thread.Sleep(500);
        }

        public Task DisposeAsync()
        {
            Repo.Dispose();
            Subject.Dispose();
            GitDb.Dispose();
            deleteReadOnlyDirectory(_localPath);
            return Task.CompletedTask;
        }

        Func<T, Task> addToList<T>(List<T> list) => 
            ev =>
            {   
                list.Add(ev);
                return Task.CompletedTask;
            };
    }
}