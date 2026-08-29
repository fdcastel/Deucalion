using System.Text.Json;
using System.Text.Json.Serialization;
using Deucalion.Application.Yaml;
using Deucalion.Configuration;
using Deucalion.Network.Configuration;
using SharpYaml;
using SharpYaml.Serialization;

namespace Deucalion.Application.Configuration;

public record ApplicationConfiguration
{
    public record ConfigurationDefaults : PullMonitorConfiguration
    {
        public TimeSpan? IntervalToDown { get; set; }

        public DnsMonitorOptionalConfiguration? Dns { get; set; }
        public HttpMonitorOptionalConfiguration? Http { get; set; }
        public PullMonitorConfiguration? Ping { get; set; }
        public PullMonitorConfiguration? Tcp { get; set; }
    }

    public static class Messages
    {
        public const string ConfigurationFileNotFound = "Configuration file '{0}' not found.";
        public const string ConfigurationFileParseError = "Error parsing configuration file '{0}': {1}";
        public const string ConfigurationMustNotBeEmpty = "Configuration file must not be empty.";
        public const string ConfigurationMustHaveMonitorsSection = "Configuration file must have a 'monitors' section.";

        public const string ConfigurationMonitorCannotBeEmpty = "Monitor '{0}' cannot be empty.";
        public const string ConfigurationInvalidTimeSpan = "Monitor '{0}': '{1}' must be a positive value, but was '{2}'.";
        public const string ConfigurationUnknownMonitorType = "Monitor '{0}': missing or unknown type tag. Expected one of: !ping, !tcp, !dns, !http, !checkin.";
        public const string ConfigurationReservedMonitorName = "Monitor '{0}': the name is reserved (it would clash with the '/api/monitors/{1}' route). Choose another name.";
    }

    /// <summary>
    /// Monitor names that collide with literal segments under <c>/api/monitors/</c>. A monitor
    /// named 'events' was reachable in the UI but not at <c>/api/monitors/events</c>, which the
    /// SSE stream owns (#23). Compared case-insensitively: routing is case-insensitive too.
    /// </summary>
    public static readonly IReadOnlySet<string> ReservedMonitorNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "events" };

    public ConfigurationDefaults? Defaults { get; set; }

    public required OrderedDictionary<string, PullMonitorConfiguration> Monitors { get; set; }

    public static ApplicationConfiguration ReadFromFile(string configurationFile)
    {
        if (!File.Exists(configurationFile))
        {
            throw new ConfigurationErrorException(string.Format(Messages.ConfigurationFileNotFound, configurationFile));
        }

        var content = File.ReadAllText(configurationFile);
        try
        {
            return ReadFromString(content);
        }
        catch (ConfigurationErrorException ex) when (ex.InnerException is YamlException)
        {
            // Re-wrap so the message names the offending file.
            throw new ConfigurationErrorException(
                string.Format(Messages.ConfigurationFileParseError, configurationFile, ex.InnerException.Message), ex.InnerException);
        }
    }

    public static ApplicationConfiguration ReadFromString(string content)
    {
        try
        {
            return Parse(content);
        }
        catch (YamlException ex)
        {
            // SharpYaml validates 'required' members at deserialization time. Surface those --
            // and any other malformed-document error -- as a configuration error, so both entry
            // points report the same exception type.
            throw new ConfigurationErrorException(string.Format(Messages.ConfigurationFileParseError, "<string>", ex.Message), ex);
        }
    }

    private static ApplicationConfiguration Parse(string content)
    {
        var options = new YamlSerializerOptions
        {
            TypeInfoResolver = DeucalionYamlContext.Default,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            Converters = [new IPEndPointConverter(), new HttpMethodConverter()],
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase,
            },
        };

        var result = YamlSerializer.Deserialize<ApplicationConfiguration>(content, options)
            ?? throw new ConfigurationErrorException(Messages.ConfigurationMustNotBeEmpty);

        result.Monitors = result.Monitors
            ?? throw new ConfigurationErrorException(Messages.ConfigurationMustHaveMonitorsSection);

        foreach (var monitor in result.Monitors)
        {
            // Check monitor is not empty
            if (monitor.Value is null)
            {
                throw new ConfigurationErrorException(string.Format(Messages.ConfigurationMonitorCannotBeEmpty, monitor.Key));
            }

            // The serializer runs with UnknownDerivedTypeHandling.FallBackToBase (see
            // DeucalionYamlContext), so a typo'd or missing tag silently deserializes to the base
            // type. Left alone it passes validation and blows up later while building the monitors.
            if (monitor.Value.GetType() == typeof(PullMonitorConfiguration))
            {
                throw new ConfigurationErrorException(string.Format(Messages.ConfigurationUnknownMonitorType, monitor.Key));
            }

            if (ReservedMonitorNames.Contains(monitor.Key))
            {
                throw new ConfigurationErrorException(string.Format(Messages.ConfigurationReservedMonitorName, monitor.Key, monitor.Key.ToLowerInvariant()));
            }

            // Interpolate ${MONITOR_NAME} placeholders
            InterpolateMonitorName(monitor.Key, monitor.Value);

            // Set monitor name
            monitor.Value.Name = monitor.Key;

            // Apply user-configured defaults
            if (result.Defaults is not null)
            {
                ApplyDefaults(result.Defaults, monitor.Value);
            }

            // Validate TimeSpan fields are positive when set
            ValidateTimeSpan(monitor.Key, nameof(monitor.Value.IntervalWhenUp), monitor.Value.IntervalWhenUp);
            ValidateTimeSpan(monitor.Key, nameof(monitor.Value.IntervalWhenDown), monitor.Value.IntervalWhenDown);
            ValidateTimeSpan(monitor.Key, nameof(monitor.Value.Timeout), monitor.Value.Timeout);
            ValidateTimeSpan(monitor.Key, nameof(monitor.Value.WarnTimeout), monitor.Value.WarnTimeout);

            if (monitor.Value is CheckInMonitorConfiguration checkIn)
            {
                ValidateTimeSpan(monitor.Key, nameof(checkIn.IntervalToDown), checkIn.IntervalToDown);
            }

            // Validate DataAnnotations (e.g. [Required] fields)
            ValidateDataAnnotations(monitor.Key, monitor.Value);
        }

        return result;
    }

    /// <summary>
    /// Fills unset fields from the 'defaults' block.
    /// </summary>
    /// <remarks>
    /// Order is load-bearing. The per-type blocks run first, so 'defaults.http.timeout' wins;
    /// the global block's '??=' then fills only what is still null. The repeated Timeout /
    /// WarnTimeout lines below look like duplication but are what encodes that precedence.
    /// </remarks>
    private static void ApplyDefaults(ConfigurationDefaults defaults, PullMonitorConfiguration monitorConfiguration)
    {
        if (defaults.Dns is not null && monitorConfiguration is DnsMonitorConfiguration dnsMonitorConfiguration)
        {
            dnsMonitorConfiguration.Timeout ??= defaults.Dns.Timeout;
            dnsMonitorConfiguration.WarnTimeout ??= defaults.Dns.WarnTimeout;

            dnsMonitorConfiguration.RecordType ??= defaults.Dns.RecordType;
            dnsMonitorConfiguration.Resolver ??= defaults.Dns.Resolver;
        }

        if (defaults.Http is not null && monitorConfiguration is HttpMonitorConfiguration httpMonitorConfiguration)
        {
            httpMonitorConfiguration.Timeout ??= defaults.Http.Timeout;
            httpMonitorConfiguration.WarnTimeout ??= defaults.Http.WarnTimeout;

            httpMonitorConfiguration.ExpectedStatusCode ??= defaults.Http.ExpectedStatusCode;
            httpMonitorConfiguration.ExpectedResponseBodyPattern ??= defaults.Http.ExpectedResponseBodyPattern;
            httpMonitorConfiguration.IgnoreCertificateErrors ??= defaults.Http.IgnoreCertificateErrors;
            httpMonitorConfiguration.Method ??= defaults.Http.Method;
        }

        if (defaults.Ping is not null && monitorConfiguration is PingMonitorConfiguration pingMonitorConfiguration)
        {
            pingMonitorConfiguration.Timeout ??= defaults.Ping.Timeout;
            pingMonitorConfiguration.WarnTimeout ??= defaults.Ping.WarnTimeout;
        }

        if (defaults.Tcp is not null && monitorConfiguration is TcpMonitorConfiguration tcpMonitorConfiguration)
        {
            tcpMonitorConfiguration.Timeout ??= defaults.Tcp.Timeout;
            tcpMonitorConfiguration.WarnTimeout ??= defaults.Tcp.WarnTimeout;
        }

        // Global defaults, applied to every monitor type.
        monitorConfiguration.Timeout ??= defaults.Timeout;
        monitorConfiguration.WarnTimeout ??= defaults.WarnTimeout;
        monitorConfiguration.IntervalWhenDown ??= defaults.IntervalWhenDown;
        monitorConfiguration.IntervalWhenUp ??= defaults.IntervalWhenUp;

        if (monitorConfiguration is CheckInMonitorConfiguration checkInMonitorConfiguration)
        {
            checkInMonitorConfiguration.IntervalToDown ??= defaults.IntervalToDown;
        }
    }

    private static void ValidateTimeSpan(string monitorName, string fieldName, TimeSpan? value)
    {
        if (value.HasValue && value.Value <= TimeSpan.Zero)
        {
            throw new ConfigurationErrorException(string.Format(Messages.ConfigurationInvalidTimeSpan, monitorName, fieldName, value.Value));
        }
    }

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(value))]
    private static string? Interpolate(string monitorName, string? value) =>
        value is not null && value.Contains("${MONITOR_NAME}", StringComparison.OrdinalIgnoreCase)
            ? value.Replace("${MONITOR_NAME}", monitorName, StringComparison.OrdinalIgnoreCase)
            : value;

    private static void InterpolateMonitorName(string monitorName, PullMonitorConfiguration monitor)
    {
        // Base PullMonitorConfiguration string properties
        monitor.Group = Interpolate(monitorName, monitor.Group);
        monitor.Href = Interpolate(monitorName, monitor.Href);

        // Derived type-specific string properties
        switch (monitor)
        {
            case CheckInMonitorConfiguration checkIn:
                checkIn.Secret = Interpolate(monitorName, checkIn.Secret);
                break;

            case DnsMonitorConfiguration dns:
                dns.Host = Interpolate(monitorName, dns.Host)!;
                break;

            case HttpMonitorConfiguration http:
                http.Url = Interpolate(monitorName, http.Url)!;
                http.ExpectedResponseBodyPattern = Interpolate(monitorName, http.ExpectedResponseBodyPattern);
                break;

            case PingMonitorConfiguration ping:
                ping.Host = Interpolate(monitorName, ping.Host)!;
                break;

            case TcpMonitorConfiguration tcp:
                tcp.Host = Interpolate(monitorName, tcp.Host)!;
                break;
        }
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "All validated types are preserved by SharpYaml source generator.")]
    private static void ValidateDataAnnotations(string monitorName, PullMonitorConfiguration monitor)
    {
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(monitor);
        try
        {
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(monitor, context, true);
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException ex)
        {
            throw new ConfigurationErrorException($"Monitor '{monitorName}': {ex.Message}", ex);
        }
    }
}
