using System.Linq;
using LibGit2Sharp;
using Newtonsoft.Json;

namespace Ylp.GitDb.Local
{
    internal static class Extensions
    {
        public static T As<T>(this string source) =>
            JsonConvert.DeserializeObject<T>(source);
    }
}
