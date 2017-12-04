using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Appy.GitDb.Tests.GitDb
{
    public class TaggingABranch : WithRepo
    {
        const string Branch = "master";
        const string TagName = "MyTag";

        protected override async Task Because()
        {
            await Subject.Save(Branch, "msg", new Document { Key = "key", Value = "value" }, Author);
            await Subject.Tag(new Reference { Name = TagName, Pointer = Branch });
        }

        [Fact]
        public void CreatesANewTagAtTheSameCommitAsTheBranch() =>
            Repo.Branches[Branch].Tip.Should().Be(Repo.Tags[TagName].Target);
    }

    public class TaggingACommit : WithRepo
    {
        const string TagName = "MyTag";
        string _sha;

        protected override async Task Because()
        {
            await Subject.Save("master", "msg", new Document { Key = "key", Value = "value" }, Author);
            _sha = Repo.Branches["master"].Tip.Sha;
            await Subject.Tag(new Reference { Name = TagName, Pointer = _sha });
        }

        [Fact]
        public void CreatesANewTagPointingAtTheCommit() =>
            Repo.Tags[TagName].Target.Sha.Should().Be(_sha);
    }

    public class TaggingATag : WithRepo
    {
        const string OriginalTagName = "MyFirstTag";
        const string TagName = "MySecondTag";

        protected override async Task Because()
        {
            await Subject.Save("master", "msg", new Document { Key = "key", Value = "value" }, Author);
            await Subject.Tag(new Reference { Name = OriginalTagName, Pointer = Repo.Branches["master"].Tip.Sha });
            await Subject.Tag(new Reference { Name = TagName, Pointer = OriginalTagName });
        }

        [Fact]
        public void CreatesANewTagPointingAtTheSameTag() =>
            Repo.Tags[TagName].Target.Should().Be(Repo.Tags[OriginalTagName].Target);
    }
}
