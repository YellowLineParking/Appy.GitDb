using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GitTest.RemoteGitDb
{
    static class Extensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items, int maxItems) =>
            items.Select((item, inx) => new { item, inx })
                 .GroupBy(x => x.inx / maxItems)
                 .Select(g => g.Select(x => x.item));

        public static async Task<T> GetAsync<T>(this HttpClient client, string url) where T : class
        {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await client.GetAsync(url).WhenSuccessful();
            var result = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(result);
            }

        public static Task<HttpResponseMessage> PostAsync<T>(this HttpClient client, string url, T item) =>
            client.PostAsync(url, new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json"));

        public static async Task<T> JsonResponse<T>(this Task<HttpResponseMessage> task) =>
            JsonConvert.DeserializeObject<T>(await (await task).Content.ReadAsStringAsync());

        public static async Task<string> AsStringResponse(this Task<HttpResponseMessage> task) =>
            await (await task).Content.ReadAsStringAsync();

        public static async Task<HttpResponseMessage> WhenSuccessful(this Task<HttpResponseMessage> task)
        {
            var response = await task;
            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new ArgumentException($"The request was not valid: {response.StatusCode}:{response.ReasonPhrase}");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException($"The request was not authorized:{response.StatusCode}:{response.ReasonPhrase}");
            if(response.StatusCode == HttpStatusCode.InternalServerError)
                throw new Exception($"An unexpected error occurred:{response.StatusCode}:{await response.Content.ReadAsStringAsync()}");
            return response;
        }
        
    }
}
