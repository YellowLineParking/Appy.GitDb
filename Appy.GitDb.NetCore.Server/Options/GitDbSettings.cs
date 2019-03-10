using System.Collections.Generic;

namespace Appy.GitDb.NetCore.Server.Settings
{
    public class GitDbSettings
    {
        public string ServerUrl { get; set; }
        public int TransactionsTimeout { get; set; }
        public List<GitDbUserSetting> Users { get; set; }
        public string GitPath { get; set; }
        public string GitHomePath { get; set; }        
        public GitDbRemoteSettings Remote { get; set; }
    }

    public class GitDbUserSetting
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public List<string> Roles { get; set; }
    }

    public class GitDbRemoteSettings
    {
        public string Url { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}