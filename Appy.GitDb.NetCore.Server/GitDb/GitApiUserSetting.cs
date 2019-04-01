using System.Collections.Generic;

namespace Appy.GitDb.NetCore.Server.GitDb
{
    public class GitApiUserSetting
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public List<string> Roles { get; set; }
    }
}