using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;
using Newtonsoft.Json;

namespace Appy.GitDb.Core.Interfaces
{
    public interface IGitDb : IDisposable
    {
        Task<string> Get(string branch, string key);
        Task<T> Get<T>(string branch, string key) where T : class;

        Task<IReadOnlyCollection<T>> GetFiles<T>(string branch, string key);
        Task<IReadOnlyCollection<string>> GetFiles(string branch, string key);

        Task<PagedFiles<T>> GetFilesPaged<T>(string branch, string key, int start, int pageSize);
        Task<PagedFiles<string>> GetFilesPaged(string branch, string key, int start, int pageSize);
        
        Task<string> Save(string branch, string message, Document document, Author author);
        Task<string> Save<T>(string branch, string message, Document<T> document, Author author);

        Task<string> Delete(string branch, string key, string message, Author author);

        Task Tag(Reference reference);
        Task DeleteTag(string tag);
        Task CreateBranch(Reference reference);
        Task<IEnumerable<string>> GetAllBranches();
        Task<ITransaction> CreateTransaction(string branch);
        Task CloseTransactions(string branch);

        Task<MergeInfo> MergeBranch(string source, string target, Author author, string message);
        Task<RebaseInfo> RebaseBranch(string source, string target, Author author, string message);
        Task DeleteBranch(string branch);

        Task<Diff> Diff(string reference, string reference2);

        Task<List<CommitInfo>> Log(string reference, string reference2);
    }

    public class PagedFiles<T>
    {
        public int Total { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public IReadOnlyCollection<T> Files { get; set; }
    }

    public static class PagingExtensions
    {
        public static PagedFiles<T> As<T>(this PagedFiles<string> pagedFiles) =>
            new PagedFiles<T>
            {
                Total = pagedFiles.Total,
                Start = pagedFiles.Start,
                End = pagedFiles.End,
                Files = pagedFiles.Files.Select(JsonConvert.DeserializeObject<T>).ToArray()
            };


        public static async Task GetFiles(this IGitDb gitDb, string branch, string key, int pageSize, Func<IReadOnlyCollection<string>, Task> processPage)
        {
            PagedFiles<string> currentResult = null;
            do
            {
                currentResult = await gitDb.GetFilesPaged(branch, key, currentResult?.End ?? 0, pageSize);
                await processPage(currentResult.Files);
            } while (currentResult.Total > currentResult.End);
        }

        public static async Task GetFiles<T>(this IGitDb gitDb, string branch, string key, int pageSize, Func<IReadOnlyCollection<T>, Task> processPage)
        {
            PagedFiles<T> currentResult = null;
            do
            {
                currentResult = await gitDb.GetFilesPaged<T>(branch, key, currentResult?.End ?? 0, pageSize);
                await processPage(currentResult.Files);
            } while (currentResult.Total > currentResult.End);
        }

        public static Task GetFiles(this IGitDb gitDb, string branch, string key, int pageSize, Action<IReadOnlyCollection<string>> processPage) =>
            gitDb.GetFiles(branch, key, pageSize, files =>
            {
                processPage(files);
                return Task.CompletedTask;
            });

        public static Task GetFiles<T>(this IGitDb gitDb, string branch, string key, int pageSize, Action<IReadOnlyCollection<T>> processPage) =>
            gitDb.GetFiles<T>(branch, key, pageSize, files =>
            {
                processPage(files);
                return Task.CompletedTask;
            });
    }
}