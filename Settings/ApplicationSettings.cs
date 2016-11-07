namespace EmailSenderBridge.Settings
{
    public class ApplicationSettings
    {
        public ServiceBusSettings ServiceBus { get; set; }
        public SmtpSettings Smtp { get; set; }
    }
}
