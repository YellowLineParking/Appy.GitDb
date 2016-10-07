using System;
using System.Collections.Generic;
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

        [Route("{branch}/document/{key}")]
        [HttpGet]
        public async Task<IHttpActionResult> Get(string branch, string key) =>
            Ok(await _gitDb.Get(branch, key));

        [Route("{branch}/documents/{key}")]
        [HttpGet]
        public async Task<IHttpActionResult> GetFiles(string branch, string key) =>
            Ok(await _gitDb.GetFiles(branch, key));


        [Route("{branch}/document")]
        [HttpPost]
        public async Task<IHttpActionResult> Save(string branch, [FromBody] SaveRequest request) =>
            Ok(await _gitDb.Save(branch, request.Message, request.Document, request.Author));

        [Route("{branch}/document/delete")]
        [HttpPost]
        public async Task<IHttpActionResult> Delete(string branch, [FromBody] DeleteRequest request) =>
            Ok(await _gitDb.Delete(branch, request.Key, request.Message, request.Author));

        [Route("tag")]
        [HttpPost]
        public async Task<IHttpActionResult> Tag([FromBody] Reference reference)
        {
            await _gitDb.Tag(reference);
            return Ok();
        }

        [Route("branch")]
        [HttpGet]
        public async Task<IHttpActionResult> GetBranches() => 
            Ok(await _gitDb.GetAllBranches());

        [Route("branch")]
        [HttpPost]
        public async Task<IHttpActionResult> CreateBranch([FromBody] Reference reference)
        {
            await _gitDb.CreateBranch(reference);
            return Ok();
        }

        static readonly Dictionary<string, ITransaction> transactions = new Dictionary<string, ITransaction>();
        [Route("{branch}/transaction")]
        [HttpPost]
        public IHttpActionResult CreateTransaction(string branch)
        {
            var trans = _gitDb.CreateTransaction(branch);
            var transactionId = Guid.NewGuid().ToString();
            transactions.Add(transactionId, trans);
            return Ok(transactionId);
        }

        [Route("{transactionId}/add")]
        [HttpPost]
        public async Task<IHttpActionResult> AddToTransaction(string transactionId, Document document)
        {
            var transaction = transactions[transactionId];
            await transaction.Add(document);
            return Ok();
        }

        [Route("{transactionId}/addmany")]
        [HttpPost]
        public async Task<IHttpActionResult> AddToTransaction(string transactionId, List<Document> documents)
        {
            var transaction = transactions[transactionId];
            await transaction.AddMany(documents);
            return Ok();
        }

        [Route("{transactionId}/delete/{key}")]
        [HttpPost]
        public async Task<IHttpActionResult> DeleteInTransaction(string transactionId, string key)
        {
            var transaction = transactions[transactionId];
            await transaction.Delete(key);
            return Ok();
        }

        [Route("{transactionId}/deleteMany")]
        [HttpPost]
        public async Task<IHttpActionResult> DeleteInTransaction(string transactionId, List<string> keys)
        {
            var transaction = transactions[transactionId];
            await transaction.DeleteMany(keys);
            return Ok();
        }


        [Route("{transactionId}/commit")]
        [HttpPost]
        public async Task<IHttpActionResult> CommitTransaction(string transactionId, [FromBody] CommitTransaction commit)
        {
            var transaction = transactions[transactionId];
            var sha = await transaction.Commit(commit.Message, commit.Author);
            transactions.Remove(transactionId);
            return Ok(sha);
        }

        [Route("{transactionId}/abort")]
        [HttpPost]
        public async Task<IHttpActionResult> AbortTransaction(string transactionId)
        {
            var transaction = transactions[transactionId];
            await transaction.Abort();
            transactions.Remove(transactionId);
            return Ok();
        }
    }
}