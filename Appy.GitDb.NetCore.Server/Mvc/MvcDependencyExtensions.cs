using Appy.GitDb.NetCore.Server.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Appy.GitDb.NetCore.Server.Mvc
{
    public static class MvcDependencyExtensions
    {
        public static IServiceCollection AddCustomMvc(this IServiceCollection services)
        {
            services
                .AddMvc(o => o.Filters.Add<HttpGlobalExceptionFilter>())
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            return services;
        }
    }
}