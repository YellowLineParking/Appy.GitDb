using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Appy.GitDb.Tests.GitDb
{
    public class DeletingAValue : WithRepo
    {
        const string Message1 = "added a file";
        const string Message2 = "Deleted a file";
        const string Branch = "master";
        const string Key = "Key";
        const string Value = "Test Value";

        protected override async Task Because()
        {
            await Subject.Save(Branch, Message1, new Document {Key = Key, Value = Value}, Author);
            await Subject.Delete(Branch, Key, Message2, Author);
        }

        [Fact]
        public void RemovesItFromTheCurrentCommit() =>
            Subject.Get(Branch, Key).Result.Should().BeNull();

        [Fact]
        public void CreatesACommitWithTheCorrectAuthor() =>
           Repo.Branches[Branch].Tip.HasTheCorrectMetaData(Message2, Author);
    }
}
