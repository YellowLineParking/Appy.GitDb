using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;

namespace Ylp.GitDb.Watcher
{
    public class BranchDictionary : IEnumerable<BranchInfo>
    {
        readonly Dictionary<string, string> _source;

        public BranchDictionary(Dictionary<string, string> source)
        {
            _source = source;
        }

        public bool HasBranch(string name) => _source.ContainsKey(name);

        public IEnumerator<BranchInfo> GetEnumerator() => new BranchInfoEnumerator(_source.GetEnumerator());
        
        public BranchInfo this[string branch] => new BranchInfo {Name = branch, Commit = _source[branch]};

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class BranchInfoEnumerator : IEnumerator<BranchInfo>
    {
        readonly IEnumerator<KeyValuePair<string, string>> _source;

        public BranchInfoEnumerator(IEnumerator<KeyValuePair<string, string>> source)
        {
            _source = source;
        }
        public void Dispose() => _source.Dispose();
        public bool MoveNext() => _source.MoveNext();
        public void Reset() => _source.Reset();
        public BranchInfo Current => BranchInfo.Create(_source.Current);
        object IEnumerator.Current => BranchInfo.Create(_source.Current);
    }

    public static class Extensions
    {
        public static BranchDictionary ToBranchDictionary(this IEnumerable<Branch> source) =>
            new BranchDictionary(source.ToDictionary(b => b.FriendlyName, b => b.Tip.Sha));

        public static BranchDictionary ToBranchDictionary(this IEnumerable<BranchInfo> source) =>
            new BranchDictionary(source.ToDictionary(b => b.Name, b => b.Commit));

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
                action(item);
        }
    }
}
