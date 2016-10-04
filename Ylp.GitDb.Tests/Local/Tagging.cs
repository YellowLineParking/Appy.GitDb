using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Local.Utils;

namespace Ylp.GitDb.Tests.Local
{
    public class TaggingABranch : WithRepo
    {
        const string Branch = "master";
        const string TagName = "MyTag";

        public TaggingABranch()
        {
            Subject.Save(Branch, "msg", new Document { Key = "key", Value = "value" }, new Author("author", "author@mail.com"));
            Subject.Tag(new Reference {Name = TagName, Pointer = Branch}).Wait();
        }

        [Fact]
        public void CreatesANewTagAtTheSameCommitAsTheBranch() =>
            Repo.Branches[Branch].Tip.Should().Be(Repo.Tags[TagName].Target);
    }

    public class TaggingACommit : WithRepo
    {
        const string TagName = "MyTag";
        readonly string _sha;

        public TaggingACommit()
        {
            Subject.Save("master", "msg", new Document { Key = "key", Value = "value" }, new Author("author", "author@mail.com"));
            _sha = Repo.Branches["master"].Tip.Sha;
            Subject.Tag(new Reference { Name = TagName, Pointer = _sha }).Wait();
        }

        [Fact]
        public void CreatesANewTagPointingAtTheCommit() =>
            Repo.Tags[TagName].Target.Sha.Should().Be(_sha);
    }

    public class TaggingATag : WithRepo
    {
        const string OriginalTagName = "MyFirstTag";
        const string TagName = "MySecondTag";
        readonly string _sha;

        public TaggingATag()
        {
            Subject.Save("master", "msg", new Document { Key = "key", Value = "value" }, new Author("author", "author@mail.com"));
            Subject.Tag(new Reference { Name = OriginalTagName, Pointer = Repo.Branches["master"].Tip.Sha }).Wait();
            Subject.Tag(new Reference { Name = TagName, Pointer = OriginalTagName }).Wait();
        }

        [Fact]
        public void CreatesANewTagPointingAtTheSameTag() =>
            Repo.Tags[TagName].Target.Should().Be(Repo.Tags[OriginalTagName].Target);
    }
}
