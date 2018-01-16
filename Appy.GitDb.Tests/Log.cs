using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Appy.GitDb.Tests
{
    public class LogWhenThereAreChanges : WithRepo
    {
        const string BranchA = "master";
        const string TagA = "tagA";
        string _commitA;
        const string BranchB = "branch";
        const string TagB = "tagB";
        string _commitB;

        readonly List<CommitInfo> _commits = new List<CommitInfo>
        {
            new CommitInfo{Author = new Author("auth1", "email"), Message = "msg1"},
            new CommitInfo{Author = new Author("auth2", "email"), Message = "msg2"},
            new CommitInfo{Author = new Author("auth3", "email"), Message = "msg3"},
        };

        protected override async Task Because()
        {
            await Subject.CreateBranch(new Reference{Name = BranchB, Pointer = BranchA});
            _commitA = Repo.Branches[BranchA].Tip.Sha;
            await Subject.Tag(new Reference { Name = TagA, Pointer = BranchA });

            var i = 0;
            foreach (var commitInfo in _commits)
            {
                await Subject.Save(BranchB, commitInfo.Message, new Document {Key = i.ToString(), Value = "value"}, commitInfo.Author);
                i++;
            }
            _commitB = Repo.Branches[BranchB].Tip.Sha;
            await Subject.Tag(new Reference { Name = TagB, Pointer = BranchB });
        }

        [Fact]
        public async Task ShowsLogBetweenBranches() =>
            testLog(await Subject.Log(BranchB, BranchA));

        [Fact]
        public async Task ShowsLogBetweenCommits() =>
            testLog(await Subject.Log(_commitB, _commitA));

        [Fact]
        public async Task ShowsLogBetweenTags() =>
            testLog(await Subject.Log(TagB, TagA));

        void testLog(List<CommitInfo> logs) => 
            logs.ForEach(l =>
            {
                var commit = _commits[logs.IndexOf(l)];
                l.Author.Name.Should().Be(commit.Author.Name);
                l.Author.Email.Should().Be(commit.Author.Email);
                l.Message.Should().Be(commit.Message);
            });
    }

    public class LogWhenThereAreNoChanges : WithRepo
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
        public async Task ShowsNoLog() =>
            (await Subject.Log(BranchA, BranchB)).Should().BeEmpty();
    }

    public class LogWithAnInvalidReference : WithRepo
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
            Utils.Utils.Catch<ArgumentException>(() => Subject.Log(BranchA, "somebranch"));
    }
}