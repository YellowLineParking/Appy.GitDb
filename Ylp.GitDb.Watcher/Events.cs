using System;
using System.Collections.Generic;

namespace Ylp.GitDb.Watcher
{
    public class BranchEvent : EventArgs
    {
        public string BranchName { get; set; }
    }
    public class BranchAdded : BranchEvent
    {
        public string Commit { get; set; }
        public string BaseBranch { get; set; }
    }

    public class BranchChanged : BranchEvent
    {
        public List<ItemAdded> Added { get; set; }
        public List<ItemModified> Modified { get; set; }
        public List<ItemDeleted> Deleted { get; set; }
        public List<ItemRenamed> Renamed { get; set; }
    }

    public class BranchRemoved : BranchEvent{ }

    public class Initialized : EventArgs
    {
        public string[] Branches { get; set; }
    }
}