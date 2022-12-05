using System.Net;
using DnsClient;

namespace Deucalion.Monitors.Options
{
    public class DnsMonitorOptions : MonitorOptions
    {
        public static readonly QueryType DefaultRecordType = QueryType.A;

        public string HostName { get; set; } = default!;

        public QueryType? RecordType { get; set; }
        public IPEndPoint? Resolver { get; set; }

        public QueryType RecordTypeOrDefault => RecordType ?? DefaultRecordType;
    }
}
