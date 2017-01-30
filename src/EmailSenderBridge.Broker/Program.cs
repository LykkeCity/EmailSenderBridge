using System;
using System.IO;
using AzureStorage.Tables;
using Common.Log;
using EmailSenderBridge.Broker.Settings;
using EmailSenderBridge.Domain.Monitoring;
using EmailSenderBridge.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailSenderBridge.Broker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IServiceCollection serviceCollection = new ServiceCollection();

            IServiceProvider serviceProvider = ConfigureServices(serviceCollection);

            var settings = serviceProvider.GetService<IOptions<ApplicationSettings>>();

            if (!IsSettingsValid(settings.Value))
            {
                return;
            }

            Application app = serviceProvider.GetService<Application>();

            System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += context =>
            {
                app.Shutdown();
            };

            app.Run();
        }

        private static IServiceProvider ConfigureServices(IServiceCollection services)
        {
            ILoggerFactory loggerFactory = new LoggerFactory()
                .AddConsole()
                .AddDebug();

            services.AddSingleton(loggerFactory);
            services.AddLogging();

            IConfigurationRoot configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.dev.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();

            services.AddSingleton(configuration);
            services.AddOptions();
            services.Configure<ApplicationSettings>(configuration);

            var connectionStrings = configuration.GetSection("ConnStrings");
            ILog log = new LogToTableRepository(new AzureTableStorage<LogEntity>(connectionStrings["Logs"], "LogEmailSenderBridge", null));
            services.AddSingleton(log);

            services.AddSingleton<IServiceMonitoringRepository>(new ServiceMonitoringRepository(
                    new AzureTableStorage<MonitoringRecordEntity>(connectionStrings["Shared"], "Monitoring", log)));

            services.AddTransient<Application>();

            IServiceProvider serviceProvider = services.BuildServiceProvider();
            return serviceProvider;
        }

        private static bool IsSettingsValid(ApplicationSettings settings)
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(settings.ServiceBus.NamespaceUrl))
            {
                isValid = false;
                Console.WriteLine("Provide NamespaceUrl value for ServiceBus section in appsettings.json or ServiceBus:NamespaceUrl from env variable");
            }

            if (string.IsNullOrEmpty(settings.ServiceBus.PolicyName))
            {
                isValid = false;
                Console.WriteLine("Provide PolicyName value for ServiceBus section in appsettings.json or ServiceBus:PolicyName from env variable");
            }

            if (string.IsNullOrEmpty(settings.ServiceBus.Key))
            {
                isValid = false;
                Console.WriteLine("Provide Key value for ServiceBus section in appsettings.json or ServiceBus:Key from env variable");
            }

            if (string.IsNullOrEmpty(settings.ServiceBus.QueueName))
            {
                isValid = false;
                Console.WriteLine("Provide QueueName value for ServiceBus section in appsettings.json or ServiceBus:QueueName from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.Host))
            {
                isValid = false;
                Console.WriteLine("Provide Host value for Smtp section in appsettings.json or Smtp:Host from env variable");
            }

            if (settings.Smtp.Port == 0)
            {
                isValid = false;
                Console.WriteLine("Provide Post value for Smtp section in appsettings.json or Smtp:Port from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.Login))
            {
                isValid = false;
                Console.WriteLine("Provide Login value for Smtp section in appsettings.json or Smtp:Login from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.Password))
            {
                isValid = false;
                Console.WriteLine("Provide Password value for Smtp section in appsettings.json or Smtp:Password from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.From))
            {
                isValid = false;
                Console.WriteLine("Provide From value for Smtp section in appsettings.json or Smtp:From from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.DisplayName))
            {
                isValid = false;
                Console.WriteLine("Provide DisplayName value for Smtp section in appsettings.json or Smtp:DisplayName from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.LocalDomain))
            {
                isValid = false;
                Console.WriteLine("Provide LocalDomain value for Smtp section in appsettings.json or Smtp:LocalDomain from env variable");
            }

            if (string.IsNullOrEmpty(settings.ConnStrings.Logs))
            {
                isValid = false;
                Console.WriteLine("Provide Logs value for ConnStrings section in appsettings.json or ConnStrings:Logs from env variable");
            }

            if (string.IsNullOrEmpty(settings.ConnStrings.Shared))
            {
                isValid = false;
                Console.WriteLine("Provide Shared value for ConnStrings section in appsettings.json or ConnStrings:Shared from env variable");
            }

            return isValid;
        }
    }
}
