using System.Collections.Generic;

namespace Appy.GitDb.Core.Model
{    
    public enum RebaseResult
    {        
        Succeeded = 0,
        Conflicts = 1
    }

    public class RebaseInfo
    {
        public string Message { get; set; }
        public string SourceBranch { get; set; }
        public string TargetBranch { get; set; }
        public string CommitSha { get; set; }
        public RebaseResult Status { get; set; }
        public IList<ConflictInfo> Conflicts { get; set; }

        public static RebaseInfo Succeeded(string sourceBranch, string targetBranch, string commitSha) =>
            new RebaseInfo
            {
                CommitSha = commitSha,
                SourceBranch = sourceBranch,
                TargetBranch = targetBranch,
                Status = RebaseResult.Succeeded
            };
    }    
}
