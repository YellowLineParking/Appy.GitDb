using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Appy.GitDb.Server.Auth
{
    public class BasicAuthOptions : AuthenticationSchemeOptions { }

    public class BasicAuthHandler : AuthenticationHandler<BasicAuthOptions>
    {
        readonly IEnumerable<User> _users;

        public BasicAuthHandler(
            IOptionsMonitor<BasicAuthOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IEnumerable<User> users)
            : base(options, logger, encoder)
        {
            _users = users;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                return Task.FromResult(AuthenticateResult.NoResult());

            try
            {
                var header = authHeader.ToString();
                if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(AuthenticateResult.NoResult());

                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
                var separatorIndex = credentials.IndexOf(':');
                if (separatorIndex < 0)
                    return Task.FromResult(AuthenticateResult.Fail("Invalid credentials format"));

                var userName = credentials[..separatorIndex];
                var password = credentials[(separatorIndex + 1)..];

                var authentication = new Authentication(_users);
                var claims = authentication.ValidateUsernameAndPassword(userName, password).GetAwaiter().GetResult();

                if (claims == null)
                    return Task.FromResult(AuthenticateResult.Fail("Invalid username or password"));

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch (Exception ex)
            {
                return Task.FromResult(AuthenticateResult.Fail(ex));
            }
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers["WWW-Authenticate"] = "Basic realm=\"appy.gitdb\"";
            Response.StatusCode = 401;
            return Task.CompletedTask;
        }
    }
}
