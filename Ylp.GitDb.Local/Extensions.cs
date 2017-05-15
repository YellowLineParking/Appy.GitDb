using Newtonsoft.Json;

namespace Ylp.GitDb.Local
{
    static class Extensions
    {
        public static T As<T>(this string source) =>
            JsonConvert.DeserializeObject<T>(source);
    }
}
