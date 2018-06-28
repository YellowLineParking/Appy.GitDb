namespace Appy.GitDb.Core.Model
{
    public enum ConflictType
    {
        Change = 0,
        Remove = 1
    }

    public class ConflictInfo
    {
        public ConflictType Type { get; set; }
    
        public string TargetSha { get; set; }

        public string SourceSha { get; set; }

        public string Path { get; set; }
    }
}
