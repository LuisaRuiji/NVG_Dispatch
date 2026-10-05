using System;
using NVGInventory.Domain.Services;

namespace NVGInventory.Tests;

internal sealed class NoOpAuditService : IAuditService
{
    public void AddEntry(
        Guid actorUserId,
        string action,
        string entityType,
        Guid entityId,
        object? before = null,
        object? after = null,
        string? actorRole = null,
        Guid? tripId = null,
        string? reason = null,
        Guid? relatedAttachmentId = null,
        string? referenceNumber = null)
    {
    }
}
