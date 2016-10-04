using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Local.Utils;

namespace Ylp.GitDb.Tests.Local
{
    public class DeletingAValue : WithRepo
    {
        const string Message = "added a file";
        const string Branch = "master";
        const string Key = "Key";
        const string Value = "Test Value";
        readonly Author _author = new Author("author", "author@mail.com");

        public DeletingAValue()
        {
            Subject.Save(Branch, Message, new Document {Key = Key, Value = Value}, _author);
            Subject.Delete(Branch, Key, Message, _author);
        }

        [Fact]
        public void RemovesItFromTheCurrentCommit() =>
            Subject.Get(Branch, Key).Result.Should().BeNull();

        [Fact]
        public void CreatesACommitWithTheCorrectAuthor() =>
           Repo.Branches[Branch].Tip.HasTheCorrectMetaData(Message, _author);
    }
}
