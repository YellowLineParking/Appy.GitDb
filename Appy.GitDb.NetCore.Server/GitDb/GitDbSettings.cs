namespace Appy.GitDb.NetCore.Server.GitDb
{
    public class GitDbSettings
    {
        public string ServerUrl { get; set; }
        public int TransactionsTimeout { get; set; }
        public string GitPath { get; set; }
        public string GitHomePath { get; set; }        
        public GitDbRemoteSettings Remote { get; set; }
    }
}