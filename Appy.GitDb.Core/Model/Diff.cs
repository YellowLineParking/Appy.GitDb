using System.Collections.Generic;

namespace Appy.GitDb.Core.Model
{
    public class Diff
    {
        public List<ItemAdded> Added { get; set; } = new List<ItemAdded>();
        public List<ItemModified> Modified { get; set; } = new List<ItemModified>();
        public List<ItemDeleted> Deleted { get; set; } = new List<ItemDeleted>();
        public List<ItemRenamed> Renamed { get; set; } = new List<ItemRenamed>();
    }

    public class ItemAdded
    {
        public string Key { get; set; }
    }
    public class ItemDeleted : ItemAdded
    {
    }
    public class ItemModified : ItemAdded
    {
    }
    public class ItemRenamed : ItemModified
    {
        public string OldKey { get; set; }
    }
}
