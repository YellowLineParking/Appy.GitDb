using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Appy.GitDb.Server.Auth
{
    class Authentication
    {
        readonly IEnumerable<User> _users;

        public Authentication(IEnumerable<User> users)
        {
            _users = users;
        }
        public Task<IEnumerable<Claim>> ValidateUsernameAndPassword(string userName, string password) =>
                Task.FromResult(_users.FirstOrDefault(u => string.Equals(u.UserName, userName, StringComparison.CurrentCultureIgnoreCase) &&
                                                           u.Password == password)?.Claims);
    }

    public class User
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public IEnumerable<string> Roles { get; set; }
        public IEnumerable<Claim> Claims =>
            Roles.Select(r => new Claim(ClaimTypes.Role, r))
                 .Union(new[] { new Claim(ClaimTypes.Name, UserName) });
    }
}