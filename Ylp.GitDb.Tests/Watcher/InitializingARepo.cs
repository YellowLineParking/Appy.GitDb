using System.Linq;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Tests.Utils;
using Ylp.GitDb.Watcher;

namespace Ylp.GitDb.Tests.Watcher
{
    public class InitializingARepo : WithWatcher
    {
        [Fact]
        public void RaisesRepoInitializedEvent() =>
            Subject.ShouldRaise("Initialized")
                   .WithArgs<Initialized>(args => args.Branches.Contains("master"));
    }
}
