using Appy.GitDb.NetCore.Server.GitDb;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Appy.GitDb.NetCore.Server.Auth
{
    public interface IUserService
    {
        Task<User> Authenticate(string username, string password);
    }

    public class UserService : IUserService
    {
        readonly List<User> _users;

        public UserService(IOptions<GitApiAuthSettings> options) =>
            _users = buildUsers(options.Value);

        static List<User> buildUsers(GitApiAuthSettings settings) =>
            settings.Users.Select(setting => new User()
            {
                UserName = setting.UserName,
                Password = setting.Password,
                Roles = setting.Roles.Select(r => r)
            }).ToList();

        static User createUser(string userName, string password, string[] roles) =>
            new User { UserName = userName, Password = password, Roles = roles };

        public Task<User> Authenticate(string userName, string password)
        {
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.UserName, userName, StringComparison.CurrentCultureIgnoreCase) &&
                u.Password == password);

            if (user == null)
                return Task.FromResult(default(User));

            var copyUser = new User { UserName = user.UserName, Roles = user.Roles.Select(x => x) };
            return Task.FromResult(copyUser);
        }
    }
}