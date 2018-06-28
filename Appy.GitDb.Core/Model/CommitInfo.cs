using System;

namespace Appy.GitDb.Core.Model
{
    public class CommitInfo
    {
        public string Sha { get; set; }
        public Author Author { get; set; }        
        public DateTime CommitDate { get; set; }
        public string Message { get; set; }
    }
}
