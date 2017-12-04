using System.Collections.Generic;
using System.Threading.Tasks;

namespace Appy.GitDb.Core.Interfaces
{
    public interface IGitServer
    {
        Task CreateDatabase(string name);
        Task DeleteDatabase(string name);
        Task<IGitDb> GetDatabase(string name);
        Task<List<string>> GetDatabases();
    }
}