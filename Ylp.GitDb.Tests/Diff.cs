using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Utils;
using static Ylp.GitDb.Tests.Utils.Utils;

namespace Ylp.GitDb.Tests
{
    public class DiffWhenThereAreChanges : WithRepo
    {
        readonly Document[] _baseDocuments = 
        {
            new Document{Key= "a", Value="a"},
            new Document{Key= "b", Value="a"},
            new Document{Key= "c", Value="a"}
        };

        readonly string[] _deletions = {"a"};
        readonly Document[] _modifications = {new Document {Key = "b", Value = "modified"}};
        readonly Document[] _additions = {new Document {Key = "d", Value = "d"}};
        const string BranchA = "master";
        const string TagA = "tagA";
        string _commitA;
        const string BranchB = "branch";
        const string TagB = "tagB";
        string _commitB;

        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction(BranchA))
            {
                await t.AddMany(_baseDocuments);
                await t.Commit("base docs", Author);
            }
            _commitA = Repo.Branches[BranchA].Tip.Sha;
            await Subject.Tag(new Reference {Name = TagA, Pointer = BranchA});

            await Subject.CreateBranch(new Reference {Name = BranchB, Pointer = BranchA});

            using (var t = await Subject.CreateTransaction(BranchB))
            {
                await t.AddMany(_modifications);
                await t.AddMany(_additions);
                await t.DeleteMany(_deletions);
                await t.Commit("branchB", Author);
            }
            _commitB = Repo.Branches[BranchB].Tip.Sha;
            await Subject.Tag(new Reference {Name = TagB, Pointer = BranchB});
        }

        [Fact]
        public async Task ShowsDifferenceBetweenBranches() =>
            testDiff(await Subject.Diff(BranchA, BranchB));

        [Fact]
        public async Task ShowsDifferenceBetweenCommits() =>
            testDiff(await Subject.Diff(_commitA, _commitB));

        [Fact]
        public async Task ShowsDifferenceBetweenTags() =>
            testDiff(await Subject.Diff(TagA, TagB));

        void testDiff(Diff diff)
        {
            diff.Added.Select(a => a.Key).Should().BeEquivalentTo(_additions.Select(a => a.Key));
            diff.Modified.Select(a => a.Key).Should().BeEquivalentTo(_modifications.Select(a => a.Key));
            diff.Deleted.Select(a => a.Key).Should().BeEquivalentTo(_deletions);
        }
    }

    public class DiffWhenThereAreNoChanges : WithRepo
    {
        readonly Document[] _baseDocuments =
        {
            new Document{Key= "a", Value="a"},
            new Document{Key= "b", Value="a"},
            new Document{Key= "c", Value="a"}
        };

        const string BranchA = "master";
        const string BranchB = "branch";


        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction(BranchA))
            {
                await t.AddMany(_baseDocuments);
                await t.Commit("base docs", Author);
            }

            await Subject.CreateBranch(new Reference { Name = BranchB, Pointer = BranchA });
        }

        [Fact]
        public async Task ShowsNoDifferences() =>
            testDiff(await Subject.Diff(BranchA, BranchB));

        void testDiff(Diff diff)
        {
            diff.Added.Should().BeEmpty();
            diff.Modified.Should().BeEmpty();
            diff.Deleted.Should().BeEmpty();
        }
    }

    public class DiffWithAnInvalidReference : WithRepo
    {
        readonly Document[] _baseDocuments =
        {
            new Document{Key= "a", Value="a"},
            new Document{Key= "b", Value="a"},
            new Document{Key= "c", Value="a"}
        };

        const string BranchA = "master";
        const string BranchB = "branch";


        protected override async Task Because()
        {
            using (var t = await Subject.CreateTransaction(BranchA))
            {
                await t.AddMany(_baseDocuments);
                await t.Commit("base docs", Author);
            }

            await Subject.CreateBranch(new Reference { Name = BranchB, Pointer = BranchA });
        }

        [Fact]
        public Task ThrowsAnArgumentException() =>
            Catch<ArgumentException>(() => Subject.Diff(BranchA, "somebranch"));
    }
}