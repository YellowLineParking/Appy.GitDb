using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Utils;
using static Ylp.GitDb.Tests.Utils.Utils;

namespace Ylp.GitDb.Tests
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
            (await Catch<Exception>(async () => await Subject.DeleteBranch("test123"))).Should().BeNull();
    }
}
