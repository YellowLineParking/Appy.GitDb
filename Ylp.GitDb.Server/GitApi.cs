using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Ylp.GitDb.Core.Interfaces;
using Ylp.GitDb.Core.Model;

namespace Ylp.GitDb.Server
{
    public class GitApiController : ApiController
    {
        readonly IGitDb _gitDb;

        public GitApiController(IGitDb gitDb)
        {
            _gitDb = gitDb;
        }

        [Route("{branch}/document/{*key}")]
        [HttpGet]
        [Authorize(Roles = "admin, read")]
        public Task<IHttpActionResult> Get(string branch, string key) =>
            result(() => _gitDb.Get(branch, key));

        [Route("{branch}/documents/{*key}")]
        [HttpGet]
        [Authorize(Roles = "admin, read")]
        public Task<IHttpActionResult> GetFiles(string branch, string key) =>
            result(() => _gitDb.GetFiles(branch, key));

        [Route("{branch}/document")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Save(string branch, [FromBody] SaveRequest request) =>
            result(() => _gitDb.Save(branch, request.Message, request.Document, request.Author));

        [Route("{branch}/document/delete")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Delete(string branch, [FromBody] DeleteRequest request) =>
            result(() => _gitDb.Delete(branch, request.Key, request.Message, request.Author));

        [Route("tag")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Tag([FromBody] Reference reference) =>
            result(() => _gitDb.Tag(reference));

        [Route("branch")]
        [HttpGet]
        [Authorize(Roles = "admin,read")]
        public Task<IHttpActionResult> GetBranches() =>
            result(() => _gitDb.GetAllBranches());

        [Route("branch")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CreateBranch([FromBody] Reference reference) =>
            result(() => _gitDb.CreateBranch(reference));

        static readonly Dictionary<string, ITransaction> _transactions = new Dictionary<string, ITransaction>();

        [Route("{branch}/transaction")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CreateTransaction(string branch) =>
            result(async () =>
            {
                var trans = await _gitDb.CreateTransaction(branch);
                var transactionId = Guid.NewGuid().ToString();
                _transactions.Add(transactionId, trans);
                return transactionId;
            });

        [Route("{branch}")]
        [HttpDelete]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> DeleteBranch(string branch) =>
           result(() => _gitDb.DeleteBranch(branch));

        [Route("{transactionId}/add")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> AddToTransaction(string transactionId, Document document) =>
            result(() => _transactions[transactionId].Add(document));

        [Route("{transactionId}/addmany")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> AddToTransaction(string transactionId, List<Document> documents) =>
            result(() => _transactions[transactionId].AddMany(documents));


        [Route("{transactionId}/delete/{key}")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> DeleteInTransaction(string transactionId, string key) =>
            result(() => _transactions[transactionId].Delete(key));

        [Route("{transactionId}/deleteMany")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> DeleteInTransaction(string transactionId, List<string> keys) =>
            result(() => _transactions[transactionId].DeleteMany(keys));


        [Route("{transactionId}/commit")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> CommitTransaction(string transactionId, [FromBody] CommitTransaction commit) =>
            result(async () =>
            {
                var transaction = _transactions[transactionId];
                var sha = await transaction.Commit(commit.Message, commit.Author);
                _transactions.Remove(transactionId);
                return sha;
            });

        [Route("{transactionId}/abort")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> AbortTransaction(string transactionId) =>
            result(async () =>
            {
                var transaction = _transactions[transactionId];
                await transaction.Abort();
                _transactions.Remove(transactionId);
            });

        [Route("merge")]
        [HttpPost]
        [Authorize(Roles = "admin,write")]
        public Task<IHttpActionResult> Merge(MergeRequest mergeRequest) =>
           result(() =>_gitDb.MergeBranch(mergeRequest.Source, mergeRequest.Target, mergeRequest.Author, mergeRequest.Message));


        async Task<IHttpActionResult> result<T>(Func<Task<T>> action)
        {
            try
            {
                return Ok(await action());
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

        async Task<IHttpActionResult> result(Func<Task> action)
        {
            try
            {
                await action();
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