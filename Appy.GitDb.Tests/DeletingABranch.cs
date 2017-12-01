using System;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Appy.GitDb.Tests
{
    public class DeletingABranch : WithRepo
    {
        protected override async Task Because()
        {
            await Subject.CreateBranch(new Reference {Name = "test", Pointer = "master"});
            await Subject.DeleteBranch("test");
        }

        [Fact]
        public async Task DoesNotReturnTheDeletedBranch() =>
            (await Subject.GetAllBranches()).Should().NotContain("test");
    }

    public class DeletingANonExistingBranch : WithRepo
    {
        [Fact]
        public async Task DoesNotThrowAnException() =>
            (await Utils.Utils.Catch<Exception>(async () => await Subject.DeleteBranch("test123"))).Should().BeNull();
    }
}
