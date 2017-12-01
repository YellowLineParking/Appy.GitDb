using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Appy.GitDb.Core.Model;

namespace Appy.GitDb.Core.Interfaces
{
    public interface ITransaction : IDisposable
    {
        Task Add(Document document);
        Task Add<T>(Document<T> document);
        Task Delete(string key);
        Task DeleteMany(IEnumerable<string> keys);
        Task AddMany<T>(IEnumerable<Document<T>> documents);
        Task AddMany(IEnumerable<Document> documents);
        Task<string> Commit(string message, Author author);
        Task Abort();
    }
}
