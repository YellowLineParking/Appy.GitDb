using System.Collections.Generic;

namespace Appy.GitDb.Core.Model
{    
    public enum MergeResult
    {        
        Succeeded = 0,
        Conflicts = 1
    }

    public class MergeInfo
    {
        public string Message { get; set; }
        public string SourceBranch { get; set; }
        public string TargetBranch { get; set; }
        public string CommitSha { get; set; }
        public MergeResult Status { get; set; }
        public IList<ConflictInfo> Conflicts { get; set; }

        public static MergeInfo Succeded(string sourceBranch, string targetBranch, string commitSha) =>
            new MergeInfo
            {
                CommitSha = commitSha,
                SourceBranch = sourceBranch,
                TargetBranch = targetBranch,
                Status = MergeResult.Succeeded
            };
    }    
}
