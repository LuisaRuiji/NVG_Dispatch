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
            Outcome = attempt.Success ? AuthOutcomes.Success : AuthOutcomes.Failure,
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
}
