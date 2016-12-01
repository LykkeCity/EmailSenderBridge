namespace EmailSenderBridge.Broker.Settings
{
    public class ApplicationSettings
    {
        public ServiceBusSettings ServiceBus { get; set; } = new ServiceBusSettings();
        public SmtpSettings Smtp { get; set; } = new SmtpSettings();
        public ConnectionStrings ConnStrings { get; set; }
    }
}
