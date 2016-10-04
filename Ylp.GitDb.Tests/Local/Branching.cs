using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Local.Utils;

namespace Ylp.GitDb.Tests.Local
{
    public class BranchingFromABranch : WithRepo
    {
        const string Branch = "master";
        const string BranchName = "MyTag";

        public BranchingFromABranch()
        {
            Subject.Save(Branch, "msg", new Document { Key = "key", Value = "value" }, new Author("author", "author@mail.com"));
            Subject.CreateBranch(new Reference {Name = BranchName, Pointer = Branch}).Wait();
        }

        [Fact]
        public void CreatesANewTagAtTheSameCommitAsTheBranch() =>
            Repo.Branches[Branch].Tip.Should().Be(Repo.Branches[BranchName].Tip);
    }

    public class BranchingFromACommit : WithRepo
    {
        const string BranchName = "MyTag";
        readonly string _sha;

        public BranchingFromACommit()
        {
            Subject.Save("master", "msg", new Document { Key = "key", Value = "value" }, new Author("author", "author@mail.com"));
            _sha = Repo.Branches["master"].Tip.Sha;
            Subject.CreateBranch(new Reference { Name = BranchName, Pointer = _sha }).Wait();
        }

        [Fact]
        public void CreatesANewTagPointingAtTheCommit() =>
            Repo.Branches[BranchName].Tip.Sha.Should().Be(_sha);
    }

    public class BranchingFromATag : WithRepo
    {
        const string TagName = "MyFirstTag";
        const string BranchName = "MySecondTag";

        public BranchingFromATag()
        {
            Subject.Save("master", "msg", new Document { Key = "key", Value = "value" }, new Author("author", "author@mail.com"));
            Subject.Tag(new Reference { Name = TagName, Pointer = Repo.Branches["master"].Tip.Sha }).Wait();
            Subject.CreateBranch(new Reference { Name = BranchName, Pointer = TagName }).Wait();
        }

        [Fact]
        public void CreatesANewTagPointingAtTheSameTag() =>
            Repo.Branches[BranchName].Tip.Should().Be(Repo.Tags[TagName].Target);
    }

    public class GettingAllBranches : WithRepo
    {
        readonly List<string> _branches = Enumerable.Range(0, 5).Select(i => "branch" + i).ToList();
        readonly List<string> _result;

        public GettingAllBranches()
        {
            _branches.ForEach(b => Subject.CreateBranch(new Reference {Name = b, Pointer = "master"}).Wait());
            _result = Subject.GetAllBranches().Result.ToList();
            _result.Remove("master");
        }

        [Fact]
        public void RetrievesAllBranches() =>
            _result.Should().BeEquivalentTo(_branches);
    }
}
