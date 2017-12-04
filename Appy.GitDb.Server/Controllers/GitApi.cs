using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Core.Model;

namespace Appy.GitDb.Server.Controllers
{
    [RoutePrefix("data/{repo}")]
    public class RepositoryController : ApiController
    {
        readonly IGitServer _gitServer;

        public RepositoryController(IGitServer gitServer) => 
            _gitServer = gitServer;

        [Route("{branch}/document/{*key}")]
        [HttpGet]
        [Authorize(Roles = "admin, read")]
        public Task<IHttpActionResult> Get(string repo, string branch, string key) =>
            result(repo, gitDb => gitDb.Get(branch, key));

        [Route("{branch}/documents/{*key}")]
        [HttpGet]
        [Authorize(Roles = "admin, read")]
        public Task<IHttpActionResult> GetFiles(string repo, string branch, string key) =>
            result(repo, gitDb => gitDb.GetFiles(branch, key));

        [Route("{branch}/document")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Save(string repo, string branch, [FromBody] SaveRequest request) =>
            result(repo, gitDb => gitDb.Save(branch, request.Message, request.Document, request.Author));

        [Route("{branch}/document/delete")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Delete(string repo, string branch, [FromBody] DeleteRequest request) =>
            result(repo, gitDb => gitDb.Delete(branch, request.Key, request.Message, request.Author));

        [Route("{branch}/transactions/close")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CloseTransactions(string repo, string branch) =>
            result(repo, gitDb => gitDb.CloseTransactions(branch));

        [Route("tag")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Tag(string repo, [FromBody] Reference reference) =>
            result(repo, gitDb => gitDb.Tag(reference));

        [Route("branch")]
        [HttpGet]
        [Authorize(Roles = "admin,read")]
        public Task<IHttpActionResult> GetBranches(string repo) =>
            result(repo, gitDb => gitDb.GetAllBranches());

        [Route("branch")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CreateBranch(string repo, [FromBody] Reference reference) =>
            result(repo, gitDb => gitDb.CreateBranch(reference));

        static readonly Dictionary<string, ITransaction> _transactions = new Dictionary<string, ITransaction>();

        [Route("{branch}/transaction")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CreateTransaction(string repo, string branch) =>
            result(repo, async gitDb =>
            {
                var trans = await (await _gitServer.GetDatabase(repo)).CreateTransaction(branch);
                var transactionId = Guid.NewGuid().ToString();
                _transactions.Add(repo + "_" + transactionId, trans);
                return transactionId;
            });

        [Route("{branch}")]
        [HttpDelete]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> DeleteBranch(string repo, string branch) =>
           result(repo, gitDb => gitDb.DeleteBranch(branch));

        [Route("tag/{tag}")]
        [HttpDelete]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> DeleteTag(string repo, string tag) =>
            result(repo, gitDb => gitDb.DeleteTag(tag));

        [Route("{transactionId}/add")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> AddToTransaction(string repo, string transactionId, Document document) =>
            result(repo, gitDb => _transactions[repo + "_" + transactionId].Add(document));

        [Route("{transactionId}/addmany")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> AddToTransaction(string repo, string transactionId, List<Document> documents) =>
            result(repo, gitDb => _transactions[repo + "_" + transactionId].AddMany(documents));


        [Route("{transactionId}/delete/{key}")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> DeleteInTransaction(string repo, string transactionId, string key) =>
            result(repo, gitDb => _transactions[repo + "_" + transactionId].Delete(key));

        [Route("{transactionId}/deleteMany")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> DeleteInTransaction(string repo, string transactionId, List<string> keys) =>
            result(repo, gitDb => _transactions[repo + "_" + transactionId].DeleteMany(keys));


        [Route("{transactionId}/commit")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CommitTransaction(string repo, string transactionId, [FromBody] CommitTransaction commit) =>
            result(repo, async gitDb =>
            {
                var transaction = _transactions[repo + "_" + transactionId];
                var sha = await transaction.Commit(commit.Message, commit.Author);
                _transactions.Remove(repo + "_" + transactionId);
                return sha;
            });

        [Route("{transactionId}/abort")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> AbortTransaction(string repo, string transactionId) =>
            result(repo, async gitDb => 
            {
                var transaction = _transactions[repo + "_" + transactionId];
                await transaction.Abort();
                _transactions.Remove(repo + "_" + transactionId);
            });

        [Route("merge")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Merge(string repo, MergeRequest mergeRequest) =>
           result(repo, gitDb => gitDb.MergeBranch(mergeRequest.Source, mergeRequest.Target, mergeRequest.Author, mergeRequest.Message));

        [Route("diff/{reference}/{reference2}")]
        [HttpGet]
        [Authorize(Roles = "admin,read")]
        public Task<IHttpActionResult> Diff(string repo, string reference, string reference2) =>
            result(repo, gitDb => gitDb.Diff(reference, reference2));

        [HttpGet, Route("log/{reference}/{reference2}")]
        [Authorize(Roles = "admin,read")]
        public Task<IHttpActionResult> Log(string repo, string reference, string reference2) =>
            result(repo, gitDb => gitDb.Log(reference, reference2));

        async Task<IHttpActionResult> result<T>(string repo, Func<IGitDb, Task<T>> action)
        {
            try
            {
                return Ok(await action(await _gitServer.GetDatabase(repo)));
            }
            catch (ArgumentException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) {Content = new StringContent(ex.Message)});
            }
            catch (NotSupportedException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(ex.Message) });
            }
        }

        async Task<IHttpActionResult> result(string repo, Func<IGitDb, Task> action)
        {
            try
            {
                await action(await _gitServer.GetDatabase(repo));
                return Ok();
            }
            catch (ArgumentException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) {Content = new StringContent(ex.Message)});
            }
            catch (NotSupportedException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(ex.Message) });
            }
        }

    }
}