namespace Ylp.GitDb.Core.Model
{
    public class SaveRequest
    {
        public string Message { get; set; }
        public Document Document { get; set; }
        public Author Author { get; set; }
    }

    public class DeleteRequest
    {
        public string Message { get; set; }
        public string Key { get; set; }
        public Author Author { get; set; }
    }

    public class CommitTransaction
    {
        public string Message { get; set; }
        public Author Author { get; set; }
    }

    public class MergeRequest
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public Author Author { get; set; }
        public string Message { get; set; }
    }
}
