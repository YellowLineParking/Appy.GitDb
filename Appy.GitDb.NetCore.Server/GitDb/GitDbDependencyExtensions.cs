using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Local;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Appy.GitDb.NetCore.Server.GitDb
{
    public static class GitDbDependencyExtensions
    {
        public static IServiceCollection AddGitDb(this ContainerBuilder builder, IServiceCollection services)
        {
            var configuration = services.BuildServiceProvider()
                .GetService<IConfiguration>();

            var gitDbSettings = new GitDbSettings();
            configuration.Bind("GitDb", gitDbSettings);

            services.Configure<GitDbSettings>(settings => configuration.Bind("GitDb", settings));

            var repo = new LocalGitDb(
                gitDbSettings.GitHomePath,
                gitDbSettings.Remote.Url, gitDbSettings.Remote.UserName, gitDbSettings.Remote.Email, gitDbSettings.Remote.Password,
                gitDbSettings.TransactionsTimeout);

            builder.RegisterInstance(repo).As<IGitDb>().ExternallyOwned();

            return services;
        }
    }
}