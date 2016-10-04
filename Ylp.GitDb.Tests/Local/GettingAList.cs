using System.Collections.Generic;
using System.Linq;
using Castle.Core.Internal;
using FluentAssertions;
using Xunit;
using Ylp.GitDb.Core.Model;
using Ylp.GitDb.Tests.Local.Utils;

namespace Ylp.GitDb.Tests.Local
{
    public class GettingAListOfFiles : WithRepo
    {
        readonly Dictionary<string, string> _rootDocuments;
        readonly Dictionary<string, string> _subDirectoryDocuments;
        readonly IReadOnlyCollection<string> _result;
        const string Directory = "subdirectory";
        public GettingAListOfFiles()
        {
            _rootDocuments = Enumerable.Range(0, 3)
                                       .ToDictionary(i => $@"{Directory}\{i}_key", i => i + "_value");
            _subDirectoryDocuments = Enumerable.Range(0, 3)
                                               .ToDictionary(i => $@"{Directory}\subdirectory\{i}_key", i => i + "sub_value");

            _rootDocuments.ForEach(d => Subject.Save("master", "message", new Document { Key = d.Key, Value = d.Value }, new Author("author", "author@mail.com")));
            _subDirectoryDocuments.ForEach(d => Subject.Save("master", "message", new Document { Key = d.Key, Value = d.Value }, new Author("author", "author@mail.com")));

            _result = Subject.GetFiles("master", Directory).Result;
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
        readonly Dictionary<string, TestClass> _rootDocuments;
        readonly Dictionary<string, TestClass> _subDirectoryDocuments;
        readonly IReadOnlyCollection<TestClass> _result;
        const string Directory = "subdirectory";
        public GettingAListOfTypedFiles()
        {
            _rootDocuments = Enumerable.Range(0, 3)
                                       .ToDictionary(i => $@"{Directory}\{i}_key", i => new TestClass(i + "_value"));
            _subDirectoryDocuments = Enumerable.Range(0, 3)
                                               .ToDictionary(i => $@"{Directory}\subdirectory\{i}_key", i => new TestClass(i + "sub_value"));

            _rootDocuments.ForEach(d => Subject.Save("master", "message", new Document<TestClass> { Key = d.Key, Value = d.Value }, new Author("author", "author@mail.com")));
            _subDirectoryDocuments.ForEach(d => Subject.Save("master", "message", new Document<TestClass> { Key = d.Key, Value = d.Value }, new Author("author", "author@mail.com")));



            _result = Subject.GetFiles<TestClass>("master", Directory).Result;
        }

        [Fact]
        public void RetrievesAllFilesInTheList() =>
            _result.Select(r => r.Value).Should().BeEquivalentTo(_rootDocuments.Values.Select(d => d.Value));

        [Fact]
        public void DoesNotRetrieveFilesRecursively() =>
            _result.Select(r => r.Value).Should().NotContain(_subDirectoryDocuments.Values.Select(d => d.Value));
    }
}
