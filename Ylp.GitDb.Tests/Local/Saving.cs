using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Local.Utils;

namespace Ylp.GitDb.Tests.Local
{
    public class SavingATypedValue : WithRepo
    {
        const string Message = "added a file";
        const string Branch = "master";
        const string Key = "Key";
        readonly TestClass _value = new TestClass("value");
        readonly Author _author = new Author("author", "author@mail.com");

        public SavingATypedValue()
        {
            Subject.Save(Branch, Message, new Document<TestClass>{Key = Key, Value = _value}, _author);
        }

        [Fact]
        public void CreatesACommitWithTheCorrectData() =>
            Repo.Branches[Branch].Tip.HasTheCorrectData(Key, _value);

        [Fact]
        public void CreatesACommitWithTheCorrectAuthor() =>
          Repo.Branches[Branch].Tip.HasTheCorrectMetaData(Message, _author);
    }

    public class SavingAStringValue : WithRepo
    {
        const string Message = "added a file";
        const string Branch = "master";
        const string Key = "Key";
        const string Value = "Test Value";
        readonly Author _author = new Author("author", "author@mail.com");

        public SavingAStringValue()
        {
            Subject.Save(Branch, Message, new Document { Key = Key, Value = Value }, _author);
        }

        [Fact]
        public void CreatesACommitWithTheCorrectData() =>
            Repo.Branches[Branch].Tip.HasTheCorrectData(Key, Value);

        [Fact]
        public void CreatesACommitWithTheCorrectAuthor() =>
           Repo.Branches[Branch].Tip.HasTheCorrectMetaData(Message, _author);
    }
}
