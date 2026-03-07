using System;

namespace NVGInventory.Domain.Services;

public interface IAuditService
{
    void AddEntry(
        Guid actorUserId,
        string action,
        string entityType,
        Guid entityId,
        object? before = null,
        object? after = null,
        string? actorRole = null,
        Guid? tripId = null);
}
