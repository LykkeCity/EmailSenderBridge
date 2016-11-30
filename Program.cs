using System;
using System.IO;
using EmailSenderBridge.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailSenderBridge
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IServiceCollection serviceCollection = new ServiceCollection();

            ConfigureServices(serviceCollection);

            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            var settings = serviceProvider.GetService<IOptions<ApplicationSettings>>();

            if (!IsSettingsValid(settings.Value))
            {
                return;
            }

            Application app = serviceProvider.GetService<Application>();
            app.Run();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            ILoggerFactory loggerFactory = new LoggerFactory()
                .AddConsole()
                .AddDebug();

            services.AddSingleton(loggerFactory);
            services.AddLogging();

            IConfigurationRoot configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

            services.AddSingleton(configuration);
            services.AddOptions();
            services.Configure<ApplicationSettings>(configuration);
            services.AddTransient<Application>();
        }

        private static bool IsSettingsValid(ApplicationSettings settings)
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(settings.ServiceBus.NamespaceUrl))
            {
                isValid = false;
                Console.WriteLine("Provide NamespaceUrl value for ServiceBus in appsettings.json or ServiceBus:NamespaceUrl from env variable");
            }

            if (string.IsNullOrEmpty(settings.ServiceBus.PolicyName))
            {
                isValid = false;
                Console.WriteLine("Provide PolicyName value for ServiceBus in appsettings.json or ServiceBus:PolicyName from env variable");
            }

            if (string.IsNullOrEmpty(settings.ServiceBus.Key))
            {
                isValid = false;
                Console.WriteLine("Provide Key value for ServiceBus in appsettings.json or ServiceBus:Key from env variable");
            }

            if (string.IsNullOrEmpty(settings.ServiceBus.QueueName))
            {
                isValid = false;
                Console.WriteLine("Provide QueueName value for ServiceBus in appsettings.json or ServiceBus:QueueName from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.Host))
            {
                isValid = false;
                Console.WriteLine("Provide Host value for Smtp in appsettings.json or Smtp:Host from env variable");
            }

            if (settings.Smtp.Port == 0)
            {
                isValid = false;
                Console.WriteLine("Provide Post value for Smtp in appsettings.json or Smtp:Port from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.Login))
            {
                isValid = false;
                Console.WriteLine("Provide Login value for Smtp in appsettings.json or Smtp:Login from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.Password))
            {
                isValid = false;
                Console.WriteLine("Provide Password value for Smtp in appsettings.json or Smtp:Password from env variable");
            }

            if (string.IsNullOrEmpty(settings.Smtp.From))
            {
                isValid = false;
                Console.WriteLine("Provide From value for Smtp in appsettings.json or Smtp:From from env variable");
            }

            return isValid;
        }
    }
}
