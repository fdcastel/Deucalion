using System.Net;

namespace Deucalion.Api.Http;

internal static class DeucalionResults
{
    internal static IResult MonitorNotFound(string monitorName) =>
        Problem("monitor-not-found", "Monitor not found.", HttpStatusCode.NotFound,
            $"Monitor '{monitorName}' not found.");

    internal static IResult NotCheckInMonitor(string monitorName, string instanceUri) =>
        Problem("not-checkin-monitor", "Not a check-in monitor.", HttpStatusCode.BadRequest,
            $"'{monitorName}' is not a check-in monitor.", instanceUri);

    internal static IResult InvalidCheckInSecret(string monitorName, string instanceUri) =>
        Problem("invalid-checkin-monitor-secret", "Invalid check-in monitor secret.", HttpStatusCode.Unauthorized,
            $"Invalid secret for '{monitorName}' check-in.", instanceUri);

    private static IResult Problem(string errorType, string title, HttpStatusCode statusCode, string detail, string? instance = null) =>
        Results.Problem(
            type: $"/api/errors/{errorType}",
            title: title,
            statusCode: (int)statusCode,
            detail: detail,
            instance: instance
        );
}
