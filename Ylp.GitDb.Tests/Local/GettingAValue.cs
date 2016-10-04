using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Local.Utils;

namespace Ylp.GitDb.Tests.Local
{
    public class GettingAStringValue : WithRepo
    {
        const string Key = "key";
        const string Value = "value";
        readonly string _result;
        public GettingAStringValue()
        {
            Subject.Save("master", "message", new Document {Key = Key, Value = Value}, new Author("author", "author@mail.com"));
            _result = Subject.Get("master", Key).Result;
        }

        [Fact]
        public void RetrievesTheInsertedValue() =>
            _result.Should().Be(Value);
    }

    public class GettingATypedValue : WithRepo
    {
        const string Key = "key";
        readonly TestClass _value = new TestClass("value");
        readonly TestClass _result;
        public GettingATypedValue()
        {
            Subject.Save("master", "message", new Document<TestClass> { Key = Key, Value = _value }, new Author("author", "author@mail.com"));
            _result = Subject.Get<TestClass>("master", Key).Result;
        }

        [Fact]
        public void RetrievesTheInsertedValue() =>
            _result.Value.Should().Be(_value.Value);

        [Fact]
        public void DoesNotRetrieveTheSameReference() =>
            _result.Should().NotBe(_value);
    }
}
