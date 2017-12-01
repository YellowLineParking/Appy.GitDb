using System;

namespace Appy.GitDb.Watcher
{
    public class ItemAdded
    {
        public string Key { get; set; }
        public Func<string> GetValue { get; set; }
    }
    public class ItemDeleted : ItemAdded
    {
    }
    public class ItemModified : ItemAdded
    {
        public Func<string> GetOldValue { get; set; }
    }
    public class ItemRenamed : ItemModified
    {
        public string OldKey { get; set; }
    }
}