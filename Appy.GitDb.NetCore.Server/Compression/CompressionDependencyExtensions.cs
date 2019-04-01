using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;

namespace Appy.GitDb.NetCore.Server.Compression
{
    public static class CompressionDependencyExtensions
    {
        public static IServiceCollection AddGzipCompression(this IServiceCollection services) =>
            services
                .Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest)
                .AddResponseCompression(options => { options.Providers.Add<GzipCompressionProvider>(); }); // options.EnableForHttps = true;
    }
}