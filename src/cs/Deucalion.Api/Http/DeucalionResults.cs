using System.Net;

namespace Deucalion.Api.Http;

internal static class DeucalionResults
{
    internal static IResult MonitorNotFound(string monitorName) =>
        Results.Problem(
            type: "/api/errors/monitor-not-found",
            title: "Monitor not found.",
            statusCode: (int)HttpStatusCode.NotFound,
            detail: $"Monitor '{monitorName}' not found."
        );

    internal static IResult NotCheckInMonitor(string monitorName, string instanceUri) =>
        Results.Problem(
            type: "/api/errors/not-checkin-monitor",
            title: "Not a check-in monitor.",
            statusCode: (int)HttpStatusCode.BadRequest,
            detail: $"'{monitorName}' is not a check-in monitor.",
            instance: instanceUri
        );

    internal static IResult InvalidCheckInSecret(string monitorName, string instanceUri) =>
        Results.Problem(
            type: "/api/errors/invalid-checkin-monitor-secret",
            title: "Invalid check-in monitor secret.",
            statusCode: (int)HttpStatusCode.Unauthorized,
            detail: $"Invalid secret for '{monitorName}' check-in.",
            instance: instanceUri
        );

}
