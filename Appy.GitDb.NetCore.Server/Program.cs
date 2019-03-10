using System;
using Appy.GitDb.NetCore.Server.Logging;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.LayoutRenderers;
using NLog.Web;
using ExceptionLayoutRenderer = Appy.GitDb.NetCore.Server.Logging.ExceptionLayoutRenderer;

namespace Appy.GitDb.NetCore.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            LayoutRenderer.Register<ExceptionLayoutRenderer>("appy-exception");
            LayoutRenderer.Register<CorrelationIdLayoutRenderer>("correlationid");
            var logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
            try
            {
                logger.Debug("Starting up git server");
                CreateWebHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Stopped program because of exception");
                throw;
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) => config
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)        
                    .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment}.json", optional: true, reloadOnChange: true))
                .UseStartup<Startup>()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    //logging.AddNLogProvider();
                })
                .UseNLog();
    }
}