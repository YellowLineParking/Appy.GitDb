using System.IO;
using System.Linq;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Appy.GitDb.Tests.GitDb
{
    public class CreatingANewLocalGitDbClientInAnEmptyPath : WithRepo
    {
        [Fact]
        public void InitializesTheRepository() =>
            Directory.Exists(LocalPath).Should().BeTrue();

        [Fact]
        public void AddsAMasterBranch() =>
            Repo.Branches.Select(b => b.FriendlyName).Should().Contain("master");

        [Fact]
        public void CreatesAnInitialCommit() =>
            Repo.Branches["master"].Commits.Count().Should().Be(1);
    }
}
