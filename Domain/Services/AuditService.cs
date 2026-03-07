using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;

namespace NVGInventory.Domain.Services;

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<AuditService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        InventoryDbContext dbContext,
        ILogger<AuditService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddEntry(
        Guid actorUserId,
        string action,
        string entityType,
        Guid entityId,
        object? before = null,
        object? after = null,
        string? actorRole = null,
        Guid? tripId = null)
    {
        var traceId = _httpContextAccessor.HttpContext?.TraceIdentifier
            ?? Activity.Current?.TraceId.ToString()
            ?? "-";
        var resolvedActorRole = ResolveActorRole(actorRole);

        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            ActorRole = resolvedActorRole,
            TripId = tripId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = Serialize(before),
            AfterJson = Serialize(after),
            TraceId = traceId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(entry);

        var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
            ?? _httpContextAccessor.HttpContext?.TraceIdentifier
            ?? "-";

        _logger.LogInformation(
            "Audit {ActionName} EntityType={EntityType} EntityId={EntityId} ActorUserId={ActorUserId} TraceId={TraceId} CorrelationId={CorrelationId}",
            action,
            entityType,
            entityId,
            actorUserId,
            traceId,
            correlationId);
    }

    private static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private string? ResolveActorRole(string? actorRole)
    {
        if (!string.IsNullOrWhiteSpace(actorRole))
        {
            return actorRole;
        }

        var roles = _httpContextAccessor.HttpContext?.User?
            .FindAll(ClaimTypes.Role)
            .Select(role => role.Value)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct()
            .ToArray();

        if (roles is null || roles.Length == 0)
        {
            return null;
        }

        return string.Join(",", roles);
    }
}
