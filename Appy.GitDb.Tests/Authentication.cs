using System;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Appy.GitDb.Tests
{
    public class ReadingFromTheRepositoryWithoutAuthentication : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(None);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Get("master", "value"));
        }

        [Fact]
        public void ThrowsAnUnauthorizedAccessException() =>
            _exception.Should().NotBeNull();
    }
    public class ReadingFromTheRepositoryAsWriteOnly : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(WriteOnly);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Get("master", "value"));
        }

        [Fact]
        public void ThrowsAnUnauthorizedAccessException() =>
           _exception.Should().NotBeNull();
    }
    public class ReadingFromTheRepositoryAsAReadWrite : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(ReadWrite);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Get("master", "value"));
        }

        [Fact]
        public void DoesNotThrowAnUnauthorizedAccessException() =>
            _exception.Should().BeNull();
    }
    public class ReadingFromTheRepositoryAsAReadOnly : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(ReadOnly);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Get("master", "value"));
        }

        [Fact]
        public void DoesNotThrowAnUnauthorizedAccessException() =>
            _exception.Should().BeNull();
    }
    public class ReadingFromTheRepositoryAsAdmin : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(Admin);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Get("master", "value"));
        }

        [Fact]
        public void DoesNotThrowAnUnauthorizedAccessException() =>
            _exception.Should().BeNull();
    }

    public class WritingToTheRepositoryWithoutAuthentication : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(None);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Save("master","msg", new Document {Key = "key"}, Author));
        }

        [Fact]
        public void ThrowsAnUnauthorizedAccessException() =>
            _exception.Should().NotBeNull();
    }
    public class WritingToTheRepositoryAsAReadWrite : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(ReadWrite);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Save("master", "msg", new Document { Key = "key" }, Author));
        }

        [Fact]
        public void DoesNotThrowAnUnauthorizedAccessException() =>
            _exception.Should().BeNull();
    }
    public class WritingToTheRepositoryAsWriteOnly : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(WriteOnly);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Save("master", "msg", new Document {Key = "test"}, Author));
        }

        [Fact]
        public void DoesNotThrowAnUnauthorizedAccessException() =>
            _exception.Should().BeNull();
    }
    public class WritingToTheRepositoryAsAReadOnly : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(ReadOnly);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Save("master", "msg", new Document { Key = "test" }, Author));
        }

        [Fact]
        public void ThrowsAnUnauthorizedAccessException() =>
             _exception.Should().NotBeNull();
    }
    public class WritingToTheRepositoryAsAdmin : WithRepo
    {
        Exception _exception;

        protected override async Task Because()
        {
            WithUser(Admin);
            _exception = await Utils.Utils.Catch<UnauthorizedAccessException>(() => Subject.Save("master", "msg", new Document { Key = "test" }, Author));
        }

        [Fact]
        public void DoesNotThrowAnUnauthorizedAccessException() =>
            _exception.Should().BeNull();
    }
}