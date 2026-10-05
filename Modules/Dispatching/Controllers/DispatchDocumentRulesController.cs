using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Security;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/admin/dispatch-document-rules")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
public sealed class DispatchDocumentRulesController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService _auditService;

    public DispatchDocumentRulesController(InventoryDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<DispatchDocumentRuleResponse>>> Get(CancellationToken cancellationToken) =>
        Ok((await _dbContext.DispatchDocumentRules.AsNoTracking().Where(rule => rule.IsActive)
            .OrderBy(rule => rule.TripType).ThenBy(rule => rule.Milestone).ThenBy(rule => rule.DocumentCode)
            .ToListAsync(cancellationToken)).Select(Map));

    [HttpPut("{tripType}/{milestone}")]
    public async Task<ActionResult<IReadOnlyCollection<DispatchDocumentRuleResponse>>> Replace(
        string tripType,
        DispatchDocumentMilestone milestone,
        IReadOnlyCollection<DispatchDocumentRuleInput> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0) throw new BusinessRuleViolationException("At least one active document rule is required.");
        var normalizedTripType = DispatchDocumentRules.NormalizeTripType(tripType);
        if (rules.Any(rule => string.IsNullOrWhiteSpace(rule.DocumentCode) || string.IsNullOrWhiteSpace(rule.Scope)))
            throw new BusinessRuleViolationException("Every rule requires a document code and scope.");
        var actorId = User.GetUserId();
        var now = DateTime.UtcNow;
        var previous = await _dbContext.DispatchDocumentRules
            .Where(rule => rule.TripType == normalizedTripType && rule.Milestone == milestone && rule.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var rule in previous)
        {
            rule.IsActive = false;
            rule.UpdatedAt = now;
            rule.UpdatedByUserId = actorId;
        }
        var replacements = rules.Select(input => new DispatchDocumentRule
        {
            Id = Guid.NewGuid(),
            TripType = normalizedTripType,
            Milestone = milestone,
            DocumentCode = input.DocumentCode.Trim().ToUpperInvariant(),
            Scope = input.Scope.Trim().ToUpperInvariant(),
            Direction = input.Direction,
            AlternativeGroup = string.IsNullOrWhiteSpace(input.AlternativeGroup) ? null : input.AlternativeGroup.Trim().ToUpperInvariant(),
            IsRequired = input.IsRequired,
            IsActive = true,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            UpdatedByUserId = actorId,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();
        _dbContext.DispatchDocumentRules.AddRange(replacements);
        _auditService.AddEntry(actorId, AuditActions.DispatchDocumentRuleChanged, EntityTypes.DispatchDocumentRule, replacements[0].Id,
            new { ActiveRuleIds = previous.Select(rule => rule.Id).ToList() },
            new { normalizedTripType, milestone, Rules = replacements.Select(rule => new { rule.DocumentCode, rule.Scope, rule.Direction, rule.AlternativeGroup }).ToList() },
            actorRole: RoleNames.Admin, reason: "Document rules replaced through Admin configuration.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(replacements.Select(Map));
    }

    private static DispatchDocumentRuleResponse Map(DispatchDocumentRule rule) => new(rule.Id, rule.TripType, rule.Milestone, rule.DocumentCode, rule.Scope, rule.Direction, rule.AlternativeGroup, rule.IsRequired, rule.Notes);
}

public sealed record DispatchDocumentRuleInput(string DocumentCode, string Scope, DocumentDirection Direction, string? AlternativeGroup, bool IsRequired, string? Notes);
public sealed record DispatchDocumentRuleResponse(Guid Id, string TripType, DispatchDocumentMilestone Milestone, string DocumentCode, string Scope, DocumentDirection Direction, string? AlternativeGroup, bool IsRequired, string? Notes);
