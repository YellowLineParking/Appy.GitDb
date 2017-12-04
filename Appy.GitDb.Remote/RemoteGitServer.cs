using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Appy.GitDb.Core.Interfaces;
using Newtonsoft.Json;

namespace Appy.GitDb.Remote
{
    public class RemoteGitServer : IGitServer
    {
        readonly string _userName;
        readonly string _password;
        readonly string _url;
        readonly HttpClient _client;

        public RemoteGitServer(HttpClient client) =>
            _client = client;

        public RemoteGitServer(string userName, string password, string url)
        {
            _userName = userName;
            _password = password;
            _url = url;
            _client = new HttpClient { BaseAddress = new Uri(url + "/repository") };
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}")));
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        string urlEncode(string value) =>
            HttpUtility.UrlEncode(value);

        public Task CreateDatabase(string name) => 
            _client.PostAsync(urlEncode(name), null).WhenSuccessful();

        public Task DeleteDatabase(string name) => 
            _client.DeleteAsync(urlEncode(name)).WhenSuccessful();

        public Task<IGitDb> GetDatabase(string name) => 
            Task.FromResult((IGitDb)new RemoteGitDb(name, _userName, _password, _url));

        public async Task<List<string>> GetDatabases() =>
            JsonConvert.DeserializeObject<List<string>>(await _client.GetAsync(string.Empty).WhenSuccessful().AsStringResponse());
    }
}