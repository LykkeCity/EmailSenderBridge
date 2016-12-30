using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amqp;
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
        private Session _session;
        private Connection _connection;
        private ReceiverLink _receiver;
        private bool _isRunning;
        private readonly string _connectionString;

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
            _connectionString = $"amqps://{policyName}:{key}@{_settings.ServiceBus.NamespaceUrl}/";
        }

        public void Run()
        {
            Start();

            _logger.LogInformation($"[{DateTime.UtcNow:u}] Waiting for the messages...");
            _log.WriteInfoAsync("EmailSenderBridge", "Run()", null, "Application started. Waiting for the messages...").Wait();

            while (_isRunning)
            {
                HandleMessages();
            }
        }

        public void Shutdown()
        {
            _logger.LogInformation($"[{DateTime.UtcNow:u}] Closing connections and shutdown application...");
            _log.WriteInfoAsync("EmailSenderBridge", "Run()", null, "Closing connections and shutdown application...").Wait();
            
            _receiver.Close(500);
            _session.Close(500);
            _connection.Close(500);
            Stop();
            _isRunning = false;
        }

        private void HandleMessages()
        {
            _connection = Connection.Factory.CreateAsync(new Address(_connectionString)).GetAwaiter().GetResult();
            _session = new Session(_connection);
            _receiver = new ReceiverLink(_session, "receiver-link", _settings.ServiceBus.QueueName);

            while (_isRunning)
            {
                Message message = _receiver.Receive();

                if (message == null)
                {
                    continue;
                }

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

                    _receiver.Accept(message);
                }
                catch (Exception ex)
                {
                    _receiver.Reject(message);
                    _log.WriteErrorAsync("EmailSernderBridge", "HandleMessages()", null, ex).Wait();
                    _logger.LogError($"[{DateTime.UtcNow:u}] {ex}");
                }
            }
        }

        public override async Task Execute()
        {
            var now = DateTime.UtcNow;

            var record = new MonitoringRecord
            {
                DateTime = now,
                ServiceName = MOnitoringServiceNames.EmailSenderBridge,
                Version = Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion
            };

            await _serviceMonitoringRepository.UpdateOrCreate(record);
        }
    }
}
