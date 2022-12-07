using System.Net;
using Deucalion.Monitors.Options;
using DnsClient;

namespace Deucalion.Network.Monitors.Options
{
    public class DnsMonitorOptions : PullMonitorOptions
    {
        public static readonly QueryType DefaultRecordType = QueryType.A;

        public string HostName { get; set; } = default!;

        public QueryType? RecordType { get; set; }
        public IPEndPoint? Resolver { get; set; }

        public QueryType RecordTypeOrDefault => RecordType ?? DefaultRecordType;
    }
}
