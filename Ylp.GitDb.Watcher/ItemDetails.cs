namespace Ylp.GitDb.Watcher
{
    public class ItemAdded
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
    public class ItemDeleted : ItemAdded
    {
    }
    public class ItemModified : ItemAdded
    {
        public string OldValue { get; set; }
    }
    public class ItemRenamed : ItemModified
    {
        public string OldKey { get; set; }
    }
}