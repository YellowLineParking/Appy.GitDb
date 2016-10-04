using System;
using System.IO;
using System.Linq;
using Castle.Core.Internal;
using GitTest.Core;
using LibGit2Sharp;
using Moq.AutoMock;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Local;

namespace Ylp.GitDb.Tests.Local.Utils
{
    public class WithRepo : IDisposable
    {
        protected IGitDb Subject;
        protected readonly string LocalPath = Path.GetTempPath() + Guid.NewGuid();
        protected readonly Repository Repo;
        public WithRepo()
        {
            Subject = new LocalGitDb(LocalPath, new AutoMocker().Get<ILogger>());
            Repo = new Repository(LocalPath);
        }

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
            Subject.Dispose();
            Repo.Dispose();
            deleteReadOnlyDirectory(LocalPath);
        }
    }
}
