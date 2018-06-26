using System.Collections.Generic;

namespace Appy.GitDb.Core.Model
{    
    /// <summary>
    /// The status of what happened as a result of a merge.
    /// </summary>
    public enum MergeStatus
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
        public MergeStatus Status { get; set; }        
        public IList<ConflictInfo> Conflicts { get; set; }

        public static MergeInfo NewSucceded(string sourceBranch, string targetBranch, string commitSha) =>
            new MergeInfo
            {
                CommitSha = commitSha,
                SourceBranch = sourceBranch,
                TargetBranch = targetBranch,
                Status = MergeStatus.Succeeded
            };               
    }    
}
