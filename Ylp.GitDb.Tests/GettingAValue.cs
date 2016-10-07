using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Utils;

namespace Ylp.GitDb.Tests
{
    public class GettingAStringValue : WithRepo
    {
        const string Key = "key";
        const string Value = "value";
        string _result;
        
        protected override async Task Because()
        {
            await Subject.Save("master", "message", new Document {Key = Key, Value = Value}, Author);
            _result = await Subject.Get("master", Key);
        }

        [Fact]
        public void RetrievesTheInsertedValue() =>
            _result.Should().Be(Value);
    }

    public class GettingATypedValue : WithRepo
    {
        const string Key = "key";
        readonly TestClass _value = new TestClass("value");
        TestClass _result;
        protected override async Task Because()
        {
            await Subject.Save("master", "message", new Document<TestClass> { Key = Key, Value = _value }, Author);
            _result = await Subject.Get<TestClass>("master", Key);
        }

        [Fact]
        public void RetrievesTheInsertedValue() =>
            _result.Value.Should().Be(_value.Value);

        [Fact]
        public void DoesNotRetrieveTheSameReference() =>
            _result.Should().NotBe(_value);
    }
}
