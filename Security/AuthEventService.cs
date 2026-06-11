using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;

namespace NVGInventory.Security;

public sealed class AuthEventService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly InventoryDbContext _dbContext;
    private readonly IHostEnvironment _environment;

    public AuthEventService(InventoryDbContext dbContext, IHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    public async Task LogLoginAttemptAsync(
        LoginCommand command,
        AuthAttemptResult attempt,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var username = attempt.Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            username = command.Username?.Trim();
        }

        var rolesJson = attempt.RolesSnapshot.Count > 0
            ? JsonSerializer.Serialize(attempt.RolesSnapshot, JsonOptions)
            : null;

        var authEvent = new AuthEvent
        {
            Id = Guid.NewGuid(),
            EventType = AuthEventTypes.Login,
            Outcome = attempt.RequiresMfa
                ? AuthOutcomes.Challenge
                : attempt.Success
                    ? AuthOutcomes.Success
                    : AuthOutcomes.Failure,
            ReasonCode = attempt.Success ? null : attempt.FailureReason,
            Username = string.IsNullOrWhiteSpace(username) ? null : username,
            UserId = attempt.UserId,
            RolesSnapshotJson = rolesJson,
            AuthMethod = "PASSWORD",
            MfaPerformed = false,
            MfaMethod = null,
            SessionId = null,
            TokenJti = attempt.Result?.TokenJti,
            CorrelationId = httpContext.Items["CorrelationId"]?.ToString(),
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            ClientApp = httpContext.Request.Headers["X-Client-App"].FirstOrDefault(),
            Environment = _environment.EnvironmentName,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AuthEvents.Add(authEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogMfaVerifyAttemptAsync(
        MfaVerificationAttemptResult attempt,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var authEvent = CreateAuthEvent(
            AuthEventTypes.MfaVerify,
            attempt.Success,
            attempt.FailureReason,
            attempt.Username,
            attempt.UserId,
            attempt.RolesSnapshot,
            "PASSWORD+TOTP",
            attempt.Success,
            "TOTP",
            attempt.Result?.TokenJti,
            httpContext);

        _dbContext.AuthEvents.Add(authEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogStepUpAttemptAsync(
        StepUpAttemptResult attempt,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var authEvent = CreateAuthEvent(
            AuthEventTypes.StepUp,
            attempt.Success,
            attempt.FailureReason,
            attempt.Username,
            attempt.UserId,
            attempt.RolesSnapshot,
            "TOTP",
            attempt.Success,
            "TOTP",
            attempt.Result?.TokenJti,
            httpContext);

        _dbContext.AuthEvents.Add(authEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private AuthEvent CreateAuthEvent(
        string eventType,
        bool success,
        string? failureReason,
        string? username,
        Guid? userId,
        IReadOnlyCollection<string> rolesSnapshot,
        string authMethod,
        bool mfaPerformed,
        string? mfaMethod,
        string? tokenJti,
        HttpContext httpContext)
    {
        var rolesJson = rolesSnapshot.Count > 0
            ? JsonSerializer.Serialize(rolesSnapshot, JsonOptions)
            : null;

        return new AuthEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Outcome = success ? AuthOutcomes.Success : AuthOutcomes.Failure,
            ReasonCode = success ? null : failureReason,
            Username = string.IsNullOrWhiteSpace(username) ? null : username,
            UserId = userId,
            RolesSnapshotJson = rolesJson,
            AuthMethod = authMethod,
            MfaPerformed = mfaPerformed,
            MfaMethod = mfaMethod,
            SessionId = null,
            TokenJti = tokenJti,
            CorrelationId = httpContext.Items["CorrelationId"]?.ToString(),
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            ClientApp = httpContext.Request.Headers["X-Client-App"].FirstOrDefault(),
            Environment = _environment.EnvironmentName,
            CreatedAt = DateTime.UtcNow
        };
    }
}
