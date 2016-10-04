using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Tests.Local.Utils;

namespace Ylp.GitDb.Tests.Local
{
    public class CreatingANewLocalGitDbClientInAnEmptyPath : WithRepo
    {
        [Fact]
        public void InitializesTheRepository() =>
            Directory.Exists($@"{LocalPath}\.git").Should().BeTrue();

        [Fact]
        public void AddsAMasterBranch() =>
            Repo.Branches.Select(b => b.FriendlyName).Should().Contain("master");

        [Fact]
        public void CreatesAnInitialCommit() =>
            Repo.Branches["master"].Commits.Count().Should().Be(1);
    }
}
