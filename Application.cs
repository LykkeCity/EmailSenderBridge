using System;
using System.IO;
using System.Net;
using System.Threading;
using Amqp;
using EmailSenderBridge.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace EmailSenderBridge
{
    public class Application
    {
        readonly ILogger _logger;
        readonly ApplicationSettings _settings;
        private readonly Session _session;
        private readonly Connection _connection;
        private bool _isRunning;

        public Application(ILogger<Application> logger, IOptions<ApplicationSettings> settings)
        {
            _logger = logger;
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
                _logger.LogWarning("Check ServiceBus settings in appsettings.json");
                _logger.LogError(ex.ToString());
            }
        }

        public void Run()
        {
            try
            {
                ReceiverLink receiver = new ReceiverLink(_session, "receiver-link", _settings.ServiceBus.QueueName);
                receiver.Start(5, ReceiveMessage);

                Console.WriteLine("Waiting for the messages...");
                System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += context =>
                {
                    Console.WriteLine("Closing connections and shutdown application...");
                    receiver.Close();
                    _session.Close();
                    _connection.Close();
                    _isRunning = false;
                };

                while (_isRunning)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private void ReceiveMessage(ReceiverLink receiver, Message message)
        {
            try
            {
                _logger.LogInformation("Processing message...");
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
                    _logger.LogInformation($"Sending email to {email} from {(string.IsNullOrEmpty(sender) ? _settings.Smtp.From : sender)} with subject '{subject}'");
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
                _logger.LogError(ex.ToString());
            }
        }
    }
}
