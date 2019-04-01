﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Core.Model;
using Appy.GitDb.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Appy.GitDb.Tests
{
    public class GettingAListOfFiles : WithRepo
    {
        readonly Dictionary<string, string> _rootDocuments = Enumerable.Range(0, 3)
                                                                       .ToDictionary(i => $@"{Directory}/{i}_key", i => i + "_value");
        readonly Dictionary<string, string> _subDirectoryDocuments = Enumerable.Range(0, 3)
                                                                               .ToDictionary(i => $@"{Directory}/subdirectory/{i}_key", i => i + "sub_value");
        IReadOnlyCollection<string> _result;
        const string Directory = "directory";
        protected override async Task Because()
        {
            await Task.WhenAll(_rootDocuments.Select(d => Subject.Save("master", "message", new Document { Key = d.Key, Value = d.Value }, Author)));
            await Task.WhenAll(_subDirectoryDocuments.Select(d => Subject.Save("master", "message", new Document { Key = d.Key, Value = d.Value }, Author)));
            _result = await Subject.GetFiles("master", Directory);
        }

        [Fact]
        public void RetrievesAllFilesInTheList() =>
            _result.Should().BeEquivalentTo(_rootDocuments.Values);

        [Fact]
        public void DoesNotRetrieveFilesRecursively() =>
            _result.Should().NotContain(_subDirectoryDocuments.Values);
    }

    public class GettingAListOfTypedFiles : WithRepo
    {
        readonly Dictionary<string, TestClass> _rootDocuments = Enumerable.Range(0, 3)
                                                                          .ToDictionary(i => $"{Directory}/{i}_key", i => new TestClass(i + "_value"));
        readonly Dictionary<string, TestClass> _subDirectoryDocuments = Enumerable.Range(0, 3)
                                                                                  .ToDictionary(i => $"{Directory}/subdirectory/{i}_key", i => new TestClass(i + "sub_value"));
        IReadOnlyCollection<TestClass> _result;
        const string Directory = "subdirectory";
        protected override async Task Because()
        {
            await Task.WhenAll(_rootDocuments.Select(d => Subject.Save("master", "message", new Document<TestClass> { Key = d.Key, Value = d.Value }, Author)));
            await Task.WhenAll(_subDirectoryDocuments.Select(d => Subject.Save("master", "message", new Document<TestClass> { Key = d.Key, Value = d.Value }, Author)));

            _result = await Subject.GetFiles<TestClass>("master", Directory);
        }

        [Fact]
        public void RetrievesAllFilesInTheList() =>
            _result.Select(r => r.Value).Should().BeEquivalentTo(_rootDocuments.Values.Select(d => d.Value));

        [Fact]
        public void DoesNotRetrieveFilesRecursively() =>
            _result.Select(r => r.Value).Should().NotContain(_subDirectoryDocuments.Values.Select(d => d.Value));

        [Fact]
        public void CloneRepoToNormal()
        {
            MoveToNormalRepo(@"C:\testrepo");
        }
    }

    public class GettingAListOfPagedFiles : WithRepo
    {
        readonly Dictionary<string, string> _rootDocuments = Enumerable.Range(0, 50)
            .ToDictionary(i => $@"{Directory}/{i}_key", i => i + "_value");
        readonly Dictionary<string, string> _subDirectoryDocuments = Enumerable.Range(0, 5)
            .ToDictionary(i => $@"{Directory}/subdirectory/{i}_key", i => i + "sub_value");
        List<string> _result;
        const string Directory = "directory";
        protected override async Task Because()
        {
            await Task.WhenAll(_rootDocuments.Select(d => Subject.Save("master", "message", new Document { Key = d.Key, Value = d.Value }, Author)));
            await Task.WhenAll(_subDirectoryDocuments.Select(d => Subject.Save("master", "message", new Document { Key = d.Key, Value = d.Value }, Author)));
            _result = new List<string>();
            await Subject.GetFiles("master", Directory, 5, files => _result.AddRange(files));
        }

        [Fact]
        public void RetrievesAllFilesInTheList() =>
            _result.Should().BeEquivalentTo(_rootDocuments.Values);

        [Fact]
        public void DoesNotRetrieveFilesRecursively() =>
            _result.Should().NotContain(_subDirectoryDocuments.Values);
    }
}
