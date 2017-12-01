using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Appy.GitDb.Tests
{
    public class BranchingFromABranch : WithRepo
    {
        const string Branch = "master";
        const string BranchName = "MyTag";

        protected override async Task Because()
        {
            await Subject.Save(Branch, "msg", new Document { Key = "key", Value = "value" }, Author);
            await Subject.CreateBranch(new Reference { Name = BranchName, Pointer = Branch });
        }

        [Fact]
        public void CreatesANewTagAtTheSameCommitAsTheBranch() =>
            Repo.Branches[Branch].Tip.Should().Be(Repo.Branches[BranchName].Tip);
    }

    public class BranchingFromACommit : WithRepo
    {
        const string BranchName = "MyTag";
        string _sha;

        protected override async Task Because()
        {
            await Subject.Save("master", "msg", new Document { Key = "key", Value = "value" }, Author);
            _sha = Repo.Branches["master"].Tip.Sha;
            await Subject.CreateBranch(new Reference { Name = BranchName, Pointer = _sha });
        }

        [Fact]
        public void CreatesANewBranchPointingAtTheCommit() =>
            Repo.Branches[BranchName].Tip.Sha.Should().Be(_sha);
    }

    public class BranchingFromATag : WithRepo
    {
        const string TagName = "MyFirstTag";
        const string BranchName = "MySecondTag";

        protected override async Task Because()
        {
            await Subject.Save("master", "msg", new Document { Key = "key", Value = "value" }, Author);
            await Subject.Tag(new Reference { Name = TagName, Pointer = Repo.Branches["master"].Tip.Sha });
            await Subject.CreateBranch(new Reference { Name = BranchName, Pointer = TagName });
        }

        [Fact]
        public void CreatesANewTagPointingAtTheSameTag() =>
            Repo.Branches[BranchName].Tip.Should().Be(Repo.Tags[TagName].Target);
    }

    public class GettingAllBranches : WithRepo
    {
        readonly List<string> _branches = Enumerable.Range(0, 5).Select(i => "branch" + i).ToList();
        List<string> _result;

        protected override async Task Because()
        {
            await Task.WhenAll(_branches.Select(b => Subject.CreateBranch(new Reference { Name = b, Pointer = "master" })));
            _result = (await Subject.GetAllBranches()).ToList();
            _branches.Add("master");
        }

        [Fact]
        public void RetrievesAllBranches() =>
            _result.Should().BeEquivalentTo(_branches);
    }
}
