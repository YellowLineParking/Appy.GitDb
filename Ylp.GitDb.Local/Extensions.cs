using System.Linq;
using LibGit2Sharp;
using Newtonsoft.Json;

namespace Ylp.GitDb.Local
{
    internal static class Extensions
    {
        public static bool HasChanges(this Repository repository, Tree tree1, Tree tree2)
        {
            var result = repository.Diff.Compare<TreeChanges>(tree1, tree2);
            return result.Added.Any() || 
                   result.Conflicted.Any() || 
                   result.Copied.Any() || 
                   result.Deleted.Any() || 
                   result.Modified.Any() || 
                   result.Renamed.Any() || 
                   result.TypeChanged.Any();
        }

        public static T As<T>(this string source) =>
            JsonConvert.DeserializeObject<T>(source);
    }
}
