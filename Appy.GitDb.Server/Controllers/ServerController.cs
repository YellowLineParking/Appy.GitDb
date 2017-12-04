using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Appy.GitDb.Core.Interfaces;

namespace Appy.GitDb.Server.Controllers
{
    [RoutePrefix("repository")]
    public class ServerController : ApiController
    {
        readonly IGitServer _gitServer;

        public ServerController(IGitServer gitServer) => 
            _gitServer = gitServer;

        [HttpPost, Route("{name}")]
        public Task<IHttpActionResult> CreateRepository(string name) =>
            result(() => _gitServer.CreateDatabase(name));

        [HttpDelete, Route("{name}")]
        public Task<IHttpActionResult> DeleteRepository(string name) =>
            result(() => _gitServer.DeleteDatabase(name));

        [HttpGet, Route("")]
        public Task<IHttpActionResult> GetRepositories() =>
            result(() => _gitServer.GetDatabases());


        async Task<IHttpActionResult> result(Func<Task> action)
        {
            try
            {
                await action();
                return Ok();
            }
            catch (ArgumentException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(ex.Message) });
            }
            catch (NotSupportedException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(ex.Message) });
            }
        }

        async Task<IHttpActionResult> result<T>(Func<Task<T>> action)
        {
            try
            {
                return Ok(await action());
            }
            catch (ArgumentException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(ex.Message) });
            }
            catch (NotSupportedException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(ex.Message) });
            }
        }

    }
}
