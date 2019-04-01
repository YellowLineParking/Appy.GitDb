using Appy.GitDb.NetCore.Server.GitDb;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Appy.GitDb.NetCore.Server.Auth
{
    public static class AuthDependencyExtensions
    {
        public static IServiceCollection AddBasicAuthentication(this IServiceCollection services, string schemeName = "BasicAuthentication")
        {
            var configuration = services.BuildServiceProvider()
                .GetService<IConfiguration>();

            services
                .Configure<GitApiAuthSettings>(settings => configuration.Bind("GitApiAuth", settings))
                .AddSingleton<IUserService, UserService>()
                .AddAuthentication(config =>
                {
                    config.DefaultAuthenticateScheme = schemeName;
                    config.DefaultChallengeScheme = schemeName;
                    config.DefaultScheme = schemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(schemeName, null);

            return services;
        }
    }
}