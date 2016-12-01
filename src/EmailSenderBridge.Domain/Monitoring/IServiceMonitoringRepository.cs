using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmailSenderBridge.Domain.Monitoring
{
    public static class MOnitoringServiceNames
    {
        public const string EmailSenderBridge = "EmailSenderBridge";
    }

    public interface IMonitoringRecord
    {
        string ServiceName { get; }
        DateTime DateTime { get; }
        string Version { get; }
    }

    public class MonitoringRecord : IMonitoringRecord
    {
        public string ServiceName { get; set; }
        public DateTime DateTime { get; set; }
        public string Version { get; set; }

        public static MonitoringRecord Create(string serviceName, DateTime dateTime, string version)
        {
            return new MonitoringRecord
            {
                ServiceName = serviceName,
                DateTime = dateTime,
                Version = version
            };
        }
    }

    public interface IServiceMonitoringRepository
    {
        Task<IEnumerable<IMonitoringRecord>> GetAllAsync();
        Task ScanAllAsync(Func<IEnumerable<IMonitoringRecord>, Task> chunk);
        Task UpdateOrCreate(IMonitoringRecord record);
    }
}
