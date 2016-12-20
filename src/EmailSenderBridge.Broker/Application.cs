using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Common;
using Common.Log;
using EmailSenderBridge.Broker.Settings;
using EmailSenderBridge.Domain.Monitoring;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace EmailSenderBridge.Broker
{
    public class Application : TimerPeriod
    {
        private readonly IServiceMonitoringRepository _serviceMonitoringRepository;
        readonly ILogger _logger;
        private readonly ILog _log;
        readonly ApplicationSettings _settings;
        private readonly Session _session;
        private readonly Connection _connection;
        private bool _isRunning;

        public Application(
            IServiceMonitoringRepository serviceMonitoringRepository,
            ILogger<Application> logger, 
            ILog log, 
            IOptions<ApplicationSettings> settings) 
            : base("EmailSenderBridge", 30000, log)
        {
            _serviceMonitoringRepository = serviceMonitoringRepository;
            _logger = logger;
            _log = log;
            _settings = settings.Value;
            _isRunning = true;

            string policyName = WebUtility.UrlEncode(_settings.ServiceBus.PolicyName);
            string key = WebUtility.UrlEncode(_settings.ServiceBus.Key);
            string connectionString = $"amqps://{policyName}:{key}@{_settings.ServiceBus.NamespaceUrl}/";

            try
            {
                _connection = Connection.Factory.CreateAsync(new Address(connectionString)).GetAwaiter().GetResult();
                _session = new Session(_connection);
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync("EmailSernderBridge", "Application()", null, ex).Wait();
                _logger.LogWarning($"[{DateTime.UtcNow:u}] Check ServiceBus settings in appsettings.json");
                _logger.LogError($"[{DateTime.UtcNow:u}] {ex}");
            }
        }

        public void Run()
        {
            try
            {
                ReceiverLink receiver = new ReceiverLink(_session, "receiver-link", _settings.ServiceBus.QueueName);
                _connection.Closed += OnChannelClosed;
                _session.Closed += OnChannelClosed;
                receiver.Closed += OnChannelClosed;
                receiver.Start(100, ReceiveMessage);

                _logger.LogInformation($"[{DateTime.UtcNow:u}] Waiting for the messages...");
                _log.WriteInfoAsync("EmailSenderBridge", "Run()", null, "Application started. Waiting for the messages...").Wait();
                Start();
                System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += context =>
                {
                    _logger.LogInformation($"[{DateTime.UtcNow:u}] Closing connections and shutdown application...");
                    _log.WriteInfoAsync("EmailSenderBridge", "Run()", null, "Closing connections and shutdown application...").Wait();
                    receiver.Close();
                    _session.Close();
                    _connection.Close();
                    _isRunning = false;
                    Stop();
                };

                while (_isRunning)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync("EmailSernderBridge", "Run()", null, ex).Wait();
                _logger.LogError($"[{DateTime.UtcNow:u}] {ex}");
            }
        }

        private void OnChannelClosed(AmqpObject sender, Error error)
        {
            string message = $"OnChannelClosed fired: IsClosed = {sender.IsClosed}, Error description = {sender.Error.Description}";
            _logger.LogInformation(message);
            _log.WriteInfoAsync("EmailSenderBridge", "OnChannelClosed()", null, message).Wait();
        }

        private void ReceiveMessage(ReceiverLink receiver, Message message)
        {
            try
            {
                _logger.LogInformation($"[{DateTime.UtcNow:u}] Processing message...");
                string email = message.ApplicationProperties["email"].ToString();
                string sender = message.ApplicationProperties["sender"].ToString();
                bool isHtml = Convert.ToBoolean(message.ApplicationProperties["isHtml"]);
                string subject = message.ApplicationProperties["subject"].ToString();
                bool hasAttachment = Convert.ToBoolean(message.ApplicationProperties["hasAttachment"]);

                string body = message.GetBody<string>();

                var emailMessage = new MimeMessage();

                emailMessage.From.Add(!string.IsNullOrEmpty(sender)
                    ? new MailboxAddress(string.Empty, sender)
                    : new MailboxAddress(_settings.Smtp.DisplayName, _settings.Smtp.From));

                emailMessage.To.Add(new MailboxAddress(string.Empty, email));
                emailMessage.Subject = subject;

                var messageBody = new TextPart(isHtml ? TextFormat.Html : TextFormat.Plain) { Text = body };


                if (hasAttachment)
                {
                    string contentType = message.ApplicationProperties["contentType"].ToString();
                    string filename = message.ApplicationProperties["fileName"].ToString();
                    byte[] file = (byte[])message.ApplicationProperties["file"];

                    var attachmentData = new MemoryStream(file);
                    var attachment = new MimePart(contentType)
                    {
                        ContentObject = new ContentObject(attachmentData),
                        FileName = filename
                    };

                    Multipart multipart = new Multipart("mixed") { messageBody, attachment };
                    emailMessage.Body = multipart;
                }
                else
                {
                    emailMessage.Body = messageBody;
                }

                using (var client = new SmtpClient())
                {
                    string logMessage = $"[{DateTime.UtcNow:u}] Sending email to {email} from {(string.IsNullOrEmpty(sender) ? _settings.Smtp.From : sender)} with subject '{subject}'";
                    _logger.LogInformation(logMessage);
                    _log.WriteInfoAsync("EmailSenderBridge", "ReceiveMessage()", null, logMessage).Wait();
                    client.LocalDomain = _settings.Smtp.LocalDomain;
                    client.Connect(_settings.Smtp.Host, _settings.Smtp.Port, SecureSocketOptions.None);
                    client.Authenticate(_settings.Smtp.Login, _settings.Smtp.Password);
                    client.Send(emailMessage);
                    client.Disconnect(true);
                }

                receiver.Accept(message);
            }
            catch (Exception ex)
            {
                receiver.Reject(message);
                _log.WriteErrorAsync("EmailSernderBridge", "ReceiveMessage()", null, ex).Wait();
                _logger.LogError($"[{DateTime.UtcNow:u}] {ex}");
            }
        }

        public override async Task Execute()
        {
            var record = new MonitoringRecord
            {
                DateTime = DateTime.UtcNow,
                ServiceName = MOnitoringServiceNames.EmailSenderBridge,
                Version = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion
            };

            await _serviceMonitoringRepository.UpdateOrCreate(record);
        }
    }
}
