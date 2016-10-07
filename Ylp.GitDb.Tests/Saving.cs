using System.Threading.Tasks;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Utils;

namespace Ylp.GitDb.Tests
{
    public class SavingATypedValue : WithRepo
    {
        const string Message = "added a file";
        const string Branch = "master";
        const string Key = "Key";
        readonly TestClass _value = new TestClass("value");
        
        protected override Task Because() =>
            Subject.Save(Branch, Message, new Document<TestClass>{Key = Key, Value = _value}, Author);

        [Fact]
        public void CreatesACommitWithTheCorrectData() =>
            Repo.Branches[Branch].Tip.HasTheCorrectData(Key, _value);

        [Fact]
        public void CreatesACommitWithTheCorrectAuthor() =>
            Repo.Branches[Branch].Tip.HasTheCorrectMetaData(Message, Author);
    }

    public class SavingAStringValue : WithRepo
    {
        const string Message = "added a file";
        const string Branch = "master";
        const string Key = "Key";
        const string Value = "Test Value";

        protected override Task Because() =>
            Subject.Save(Branch, Message, new Document { Key = Key, Value = Value }, Author);

        [Fact]
        public void CreatesACommitWithTheCorrectData() =>
            Repo.Branches[Branch].Tip.HasTheCorrectData(Key, Value);

        [Fact]
        public void CreatesACommitWithTheCorrectAuthor() =>
           Repo.Branches[Branch].Tip.HasTheCorrectMetaData(Message, Author);
    }
}
