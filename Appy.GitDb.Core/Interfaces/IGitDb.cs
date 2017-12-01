using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;

namespace Appy.GitDb.Core.Interfaces
{
    public interface IGitDb : IDisposable
    {
        Task<string> Get(string branch, string key);
        Task<T> Get<T>(string branch, string key) where T : class;

        Task<IReadOnlyCollection<T>> GetFiles<T>(string branch, string key);
        Task<IReadOnlyCollection<string>> GetFiles(string branch, string key);

        Task<string> Save(string branch, string message, Document document, Author author);
        Task<string> Save<T>(string branch, string message, Document<T> document, Author author);

        Task<string> Delete(string branch, string key, string message, Author author);

        Task Tag(Reference reference);
        Task DeleteTag(string tag);
        Task CreateBranch(Reference reference);
        Task<IEnumerable<string>> GetAllBranches();
        Task<ITransaction> CreateTransaction(string branch);
        Task CloseTransactions(string branch);

        Task<string> MergeBranch(string source, string target, Author author, string message);
        Task DeleteBranch(string branch);

        Task<Diff> Diff(string reference, string reference2);
    }
}