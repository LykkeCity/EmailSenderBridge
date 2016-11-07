using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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

        public Application(ILogger<Application> logger, IOptions<ApplicationSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;

            string policyName = WebUtility.UrlEncode(_settings.ServiceBus.PolicyName);
            string key = WebUtility.UrlEncode(_settings.ServiceBus.Key);
            string connnectionString = $"amqps://{policyName}:{key}@{_settings.ServiceBus.NamespaceUrl}/";

            _connection = Connection.Factory.CreateAsync(new Address(connnectionString)).GetAwaiter().GetResult();
            _session = new Session(_connection);
        }

        public async Task RunAsync()
        {
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource();

                Task.WaitAll(
                    ReceiveMessagesAsync(cts.Token)
                );

                await _session.CloseAsync();
                await _connection.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            ReceiverLink receiver = new ReceiverLink(_session, "receiver-link", _settings.ServiceBus.QueueName);

            while (!cancellationToken.IsCancellationRequested)
            {
                Message message = await receiver.ReceiveAsync(1000);

                if (message != null)
                {
                    bool isHtml = Convert.ToBoolean(message.ApplicationProperties["IsHtml"]);
                    string subject = message.ApplicationProperties["Subject"].ToString();
                    string to = message.ApplicationProperties["To"].ToString();

                    string body = message.GetBody<string>();

                    var emailMessage = new MimeMessage();

                    emailMessage.From.Add(new MailboxAddress(_settings.Smtp.DisplayName, _settings.Smtp.From));
                    emailMessage.To.Add(new MailboxAddress(string.Empty, to));
                    emailMessage.Subject = subject;

                    var messageBody = new TextPart(isHtml ? TextFormat.Html : TextFormat.Plain) { Text = body };

                    //TODO: implement adding attachments
                    //var attachmentData = new MemoryStream(Encoding.UTF8.GetBytes("MyText"));
                    //var attachment = new MimePart("text", "txt")
                    //{
                    //    ContentObject = new ContentObject(attachmentData),
                    //    FileName = "mytext.txt"
                    //};

                    //var multipart = new Multipart("mixed");
                    //multipart.Add(messageBody);
                    //multipart.Add(attachment);

                    //emailMessage.Body = multipart;

                    emailMessage.Body = messageBody;

                    using (var client = new SmtpClient())
                    {
                        client.LocalDomain = _settings.Smtp.LocalDomain;

                        await client.ConnectAsync(_settings.Smtp.Host, _settings.Smtp.Port, SecureSocketOptions.None, cancellationToken).ConfigureAwait(false);
                        await client.AuthenticateAsync(_settings.Smtp.Login, _settings.Smtp.Password, cancellationToken).ConfigureAwait(false);
                        await client.SendAsync(emailMessage, cancellationToken).ConfigureAwait(false);
                        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
                    }

                    receiver.Accept(message);
                }
            }

            await receiver.CloseAsync();
        }
    }
}
