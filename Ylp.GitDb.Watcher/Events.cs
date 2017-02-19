using System;
using System.Collections.Generic;

namespace Ylp.GitDb.Watcher
{
    public class BranchEvent : EventArgs
    {
        public BranchInfo Branch { get; set; }
    }

    public class BranchModification : BranchEvent
    {
        public List<ItemAdded> Added { get; set; }
        public List<ItemModified> Modified { get; set; }
        public List<ItemDeleted> Deleted { get; set; }
        public List<ItemRenamed> Renamed { get; set; }
    }
    public class BranchAdded : BranchModification
    {
        public string BaseBranch { get; set; }
    }

    public class BranchChanged : BranchModification
    {
        public string PreviousCommit { get; set; }
    }

    public class BranchRemoved : BranchEvent{ }

    public class BranchInfo
    {
        public string Name { get; set; }
        public string Commit { get; set; }
        public static BranchInfo Create(KeyValuePair<string, string> keyValuePair) =>
            new BranchInfo {Name = keyValuePair.Key, Commit = keyValuePair.Value};
    }
}