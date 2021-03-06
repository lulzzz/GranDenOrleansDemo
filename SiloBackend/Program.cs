﻿using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace SiloBackend
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Orleans.Runtime.Management.ManagementGrain", LogEventLevel.Warning)
                .MinimumLevel.Override("Orleans.Runtime.SiloControl", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Trace()
                .WriteTo.Debug();

            Log.Logger = logConfig
                .Enrich.FromLogContext().CreateLogger();

            try
            {
                using (var genericHost = CreateHostBuilder(args).Build())
                {
                    await genericHost.RunAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Generic host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            new HostBuilder()
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("hostsettings.json", optional: true)
                        .AddEnvironmentVariables(prefix: "ORLEANS_HOST_")
                        .AddCommandLine(args);
                })
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.AddJsonFile("appsettings.json", optional: true)
                        .AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json",
                            optional: true)
                        .AddEnvironmentVariables(prefix: "ORLEANS_HOST_APP_")
                        .AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var orleansConf = hostContext.Configuration.GetSection("Orleans");

                    services.AddOptions<TypedOptions.SiloConfigOption>().Bind(orleansConf.GetSection("SiloConfig"));
                    services.AddOptions<TypedOptions.OrleansProviderOption>().Bind(orleansConf.GetSection("Provider"));
                    services.AddOptions<TypedOptions.OrleansDashboardOption>().Bind(orleansConf.GetSection("Dashboard"));

                    services.Configure<HostOptions>(option =>
                    {
                        option.ShutdownTimeout = TimeSpan.FromSeconds(20);
                    });

                    services.AddHostedService<HostedServices.OrleansSiloHostedService>();
                })
                .UseConsoleLifetime()
                .UseSerilog();
    }
}
