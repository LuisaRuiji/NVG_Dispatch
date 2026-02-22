using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record SubmitMaintenanceIssueCommand(
    Guid RequesterUserId,
    Guid AssetId,
    string? Purpose,
    IReadOnlyCollection<RequestLineInput> Lines);

public sealed record SubmitMaintenanceIssueResult(Guid RequestId, Guid ApprovalId, RequestStatus Status);

public sealed record SubmitAdjustmentCommand(
    Guid RequesterUserId,
    string Reason,
    IReadOnlyCollection<RequestLineInput> Lines);

public sealed record SubmitAdjustmentResult(Guid RequestId, Guid ApprovalId, RequestStatus Status);

public sealed record IssueRequestCommand(Guid RequestId, Guid ActorUserId);

public sealed record IssueRequestResult(Guid RequestId, RequestStatus Status);

public sealed class RequestWorkflowService
{
    private readonly InventoryDbContext _dbContext;
    private readonly RequestService _requestService;
    private readonly ApprovalService _approvalService;
    private readonly StockLedgerService _stockLedgerService;
    private readonly UserService _userService;
    private readonly IAuditService? _auditService;
    private readonly ILogger<RequestWorkflowService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestWorkflowService(
        InventoryDbContext dbContext,
        RequestService requestService,
        ApprovalService approvalService,
        StockLedgerService stockLedgerService,
        UserService userService,
        IAuditService? auditService = null,
        ILogger<RequestWorkflowService>? logger = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _dbContext = dbContext;
        _requestService = requestService;
        _approvalService = approvalService;
        _stockLedgerService = stockLedgerService;
        _userService = userService;
        _auditService = auditService;
        _logger = logger ?? NullLogger<RequestWorkflowService>.Instance;
        _httpContextAccessor = httpContextAccessor ?? new HttpContextAccessor();
    }

    private string GetCorrelationId()
    {
        return _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
               ?? _httpContextAccessor.HttpContext?.TraceIdentifier
               ?? "-";
    }

    public async Task<SubmitMaintenanceIssueResult> SubmitMaintenanceIssueAsync(
        SubmitMaintenanceIssueCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var request = await _requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.MaintenanceIssue,
                command.RequesterUserId,
                command.AssetId,
                command.Purpose,
                command.Lines),
            RequestStatus.PendingIO,
            cancellationToken);

        request.SubmittedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        var approval = await _approvalService.CreateApprovalAsync(
            new CreateApprovalCommand(
                WorkflowKeys.MaintenanceIssueApproval,
                EntityTypes.Request,
                request.Id,
                command.RequesterUserId),
            cancellationToken);

        _auditService?.AddEntry(
            command.RequesterUserId,
            AuditActions.RequestSubmitted,
            EntityTypes.Request,
            request.Id,
            null,
            new { request.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SubmitMaintenanceIssueResult(request.Id, approval.Id, request.Status);
    }

    public async Task<SubmitAdjustmentResult> SubmitAdjustmentAsync(
        SubmitAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureUserHasRoleAsync(command.RequesterUserId, RoleNames.InventoryOfficer, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var request = await _requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.AdjustmentDamageLoss,
                command.RequesterUserId,
                null,
                command.Reason,
                command.Lines),
            RequestStatus.PendingIO,
            cancellationToken);

        request.SubmittedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        var approval = await _approvalService.CreateApprovalAsync(
            new CreateApprovalCommand(
                WorkflowKeys.AdjustmentApproval,
                EntityTypes.Request,
                request.Id,
                command.RequesterUserId),
            cancellationToken);

        _auditService?.AddEntry(
            command.RequesterUserId,
            AuditActions.RequestSubmitted,
            EntityTypes.Request,
            request.Id,
            null,
            new { request.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SubmitAdjustmentResult(request.Id, approval.Id, request.Status);
    }

    public async Task<RequestStatus> SubmitRequest(Guid requestId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var request = await _dbContext.Requests
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Request not found.");
        }

        if (request.RequestType is not (RequestType.MaintenanceIssue or RequestType.Borrow))
        {
            throw new BusinessRuleViolationException("SubmitRequest supports maintenance issue and borrow requests only.");
        }

        if (request.Status != RequestStatus.Draft)
        {
            throw new BusinessRuleViolationException("Only draft requests can be submitted.");
        }

        if (request.RequestType == RequestType.MaintenanceIssue && request.AssetId is null)
        {
            throw new BusinessRuleViolationException("Maintenance issue requests require an asset.");
        }

        if (request.AssetId.HasValue)
        {
            var asset = await _dbContext.Assets
                .FirstOrDefaultAsync(a => a.Id == request.AssetId.Value, cancellationToken);
            if (asset is null)
            {
                throw new NotFoundException("Asset not found for request.");
            }

            if (asset.Status != AssetStatus.Active)
            {
                throw new BusinessRuleViolationException("Asset is inactive.");
            }
        }

        if (request.Lines.Count == 0)
        {
            throw new BusinessRuleViolationException("Request must include at least one line.");
        }

        var requester = await _dbContext.Users.FirstOrDefaultAsync(
            user => user.Id == request.RequesterUserId,
            cancellationToken);

        if (requester is null || !requester.IsActive)
        {
            throw new BusinessRuleViolationException("Requester must be an active user.");
        }

        var inventoryIds = request.Lines.Select(line => line.InventoryId).Distinct().ToArray();
        var items = await _dbContext.InventoryItems
            .Where(item => inventoryIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (items.Count != inventoryIds.Length)
        {
            throw new NotFoundException("One or more inventory items were not found.");
        }

        if (items.Any(item => !item.IsActive))
        {
            throw new BusinessRuleViolationException("One or more inventory items are inactive.");
        }

        if (request.RequestType == RequestType.MaintenanceIssue && items.Any(item => item.ItemType != ItemType.Consumable))
        {
            throw new BusinessRuleViolationException("Maintenance issue requests can only include consumables.");
        }

        if (request.RequestType == RequestType.Borrow && items.Any(item => item.ItemType != ItemType.NonConsumable))
        {
            throw new BusinessRuleViolationException("Borrow requests can only include non-consumables.");
        }

        var workflowKey = request.RequestType == RequestType.MaintenanceIssue
            ? WorkflowKeys.MaintenanceIssueApproval
            : WorkflowKeys.BorrowApproval;

        var approval = await _dbContext.Approvals
            .FirstOrDefaultAsync(
                a => a.EntityType == EntityTypes.Request && a.EntityId == request.Id,
                cancellationToken);

        if (approval is not null)
        {
            throw new BusinessRuleViolationException("Approval already exists for this request.");
        }

        await _approvalService.CreateApprovalAsync(
            new CreateApprovalCommand(
                workflowKey,
                EntityTypes.Request,
                request.Id,
                request.RequesterUserId),
            cancellationToken);

        request.Status = RequestStatus.PendingIO;
        request.SubmittedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        _auditService?.AddEntry(
            request.RequesterUserId,
            AuditActions.RequestSubmitted,
            EntityTypes.Request,
            request.Id,
            null,
            new { request.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Action={Action} EntityId={EntityId} ActorUserId={ActorUserId} CorrelationId={CorrelationId}",
            "SubmitRequest",
            request.Id,
            request.RequesterUserId,
            GetCorrelationId());

        return request.Status;
    }

    public async Task<RequestStatus> InventoryOfficerReviewAsync(
        Guid requestId,
        Guid actorUserId,
        IReadOnlyCollection<(Guid RequestLineId, decimal QtyApproved, string? Remarks)> lines,
        string? ioRemarks,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var request = await _dbContext.Requests
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Request not found.");
        }

        if (request.Status != RequestStatus.PendingIO)
        {
            throw new BusinessRuleViolationException("Request is not pending IO review.");
        }

        await _userService.EnsureUserHasRoleAsync(actorUserId, RoleNames.InventoryOfficer, cancellationToken);

        if (request.RequestType == RequestType.MaintenanceIssue && request.AssetId is null)
        {
            throw new BusinessRuleViolationException("Maintenance issue requests require an asset.");
        }

        if (lines.Count == 0)
        {
            throw new BusinessRuleViolationException("IO review must include at least one line.");
        }

        var requestLineIds = request.Lines.Select(line => line.Id).ToHashSet();
        foreach (var line in lines)
        {
            if (!requestLineIds.Contains(line.RequestLineId))
            {
                throw new BusinessRuleViolationException("IO review includes invalid request line.");
            }
        }

        var inventoryIds = request.Lines.Select(line => line.InventoryId).Distinct().ToArray();
        var items = await _dbContext.InventoryItems
            .Where(item => inventoryIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (items.Count != inventoryIds.Length)
        {
            throw new NotFoundException("One or more inventory items were not found.");
        }

        if (request.RequestType == RequestType.MaintenanceIssue && items.Any(item => item.ItemType != ItemType.Consumable))
        {
            throw new BusinessRuleViolationException("Maintenance issue requests can only include consumables.");
        }

        if (request.RequestType == RequestType.Borrow && items.Any(item => item.ItemType != ItemType.NonConsumable))
        {
            throw new BusinessRuleViolationException("Borrow requests can only include non-consumables.");
        }

        var availability = items.ToDictionary(item => item.Id, item => item.Quantity);
        var approvedByLineId = lines.ToDictionary(line => line.RequestLineId, line => line);

        decimal totalApproved = 0m;

        foreach (var requestLine in request.Lines)
        {
            if (!approvedByLineId.TryGetValue(requestLine.Id, out var input))
            {
                throw new BusinessRuleViolationException("IO review missing request line approval.");
            }

            var available = availability[requestLine.InventoryId];
            if (request.RequestType == RequestType.AdjustmentDamageLoss)
            {
                if (input.QtyApproved % 1m != 0m)
                {
                    throw new BusinessRuleViolationException("Approved quantity must be a whole number.");
                }

                if (input.QtyApproved == 0m)
                {
                    requestLine.QtyApproved = 0m;
                    if (!string.IsNullOrWhiteSpace(input.Remarks))
                    {
                        requestLine.Remarks = input.Remarks;
                    }

                    continue;
                }

                if (Math.Sign(input.QtyApproved) != Math.Sign(requestLine.QtyRequested))
                {
                    throw new BusinessRuleViolationException("Approved quantity sign must match requested quantity.");
                }

                if (Math.Abs(input.QtyApproved) > Math.Abs(requestLine.QtyRequested))
                {
                    throw new BusinessRuleViolationException("Approved quantity cannot exceed requested quantity.");
                }

                if (input.QtyApproved < 0m && Math.Abs(input.QtyApproved) > available)
                {
                    throw new BusinessRuleViolationException("Approved quantity cannot exceed available stock for adjustment.");
                }

                requestLine.QtyApproved = input.QtyApproved;
                if (!string.IsNullOrWhiteSpace(input.Remarks))
                {
                    requestLine.Remarks = input.Remarks;
                }

                totalApproved += Math.Abs(input.QtyApproved);
            }
            else
            {
                if (input.QtyApproved < 0)
                {
                    throw new BusinessRuleViolationException("Approved quantity cannot be negative.");
                }

                if (input.QtyApproved % 1m != 0m)
                {
                    throw new BusinessRuleViolationException("Approved quantity must be a whole number.");
                }

                if (input.QtyApproved > requestLine.QtyRequested)
                {
                    throw new BusinessRuleViolationException("Approved quantity cannot exceed requested quantity.");
                }

                var maxAllowed = Math.Min(input.QtyApproved, requestLine.QtyRequested);
                var approved = Math.Min(maxAllowed, available);

                requestLine.QtyApproved = approved;
                if (!string.IsNullOrWhiteSpace(input.Remarks))
                {
                    requestLine.Remarks = input.Remarks;
                }

                availability[requestLine.InventoryId] = available - approved;
                totalApproved += approved;
            }
        }

        var approval = await _dbContext.Approvals
            .FirstOrDefaultAsync(
                a => a.EntityType == EntityTypes.Request && a.EntityId == request.Id,
                cancellationToken);

        if (approval is null)
        {
            throw new NotFoundException("Approval record not found for request.");
        }

        var decision = totalApproved > 0 ? ApprovalDecision.Approve : ApprovalDecision.Reject;
        var remarks = totalApproved > 0 ? ioRemarks : ioRemarks ?? "insufficient stock";

        var result = await _approvalService.ApplyDecisionAsync(
            approval.Id,
            actorUserId,
            decision,
            remarks,
            cancellationToken);

        _auditService?.AddEntry(
            actorUserId,
            decision == ApprovalDecision.Approve
                ? AuditActions.RequestIoReviewApproved
                : AuditActions.RequestIoReviewRejected,
            EntityTypes.Request,
            request.Id,
            null,
            new { Decision = decision.ToString(), Remarks = remarks });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Action={Action} EntityId={EntityId} ActorUserId={ActorUserId} CorrelationId={CorrelationId}",
            "InventoryOfficerReview",
            request.Id,
            actorUserId,
            GetCorrelationId());

        return result.Request?.Status ?? request.Status;
    }

    public async Task<RequestStatus> ManagerDecisionAsync(
        Guid requestId,
        Guid actorUserId,
        ApprovalDecision decision,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        if (decision == ApprovalDecision.Approve)
        {
            return await ApproveAsManager(requestId, actorUserId, remarks, cancellationToken);
        }

        return await RejectAsManager(requestId, actorUserId, remarks, cancellationToken);
    }

    private async Task<RequestStatus> ApproveAsManager(
        Guid requestId,
        Guid actorUserId,
        string? remarks,
        CancellationToken cancellationToken)
    {
        var request = await _dbContext.Requests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Request not found.");
        }

        if (request.Status != RequestStatus.PendingManager)
        {
            throw new BusinessRuleViolationException("Only pending manager requests can be approved.");
        }

        if (request.RequestType == RequestType.AdjustmentDamageLoss)
        {
            var hasAdjustmentLogs = await _dbContext.StockLogs
                .AsNoTracking()
                .AnyAsync(
                    log => log.RefType == EntityTypes.Request
                           && log.RefId == request.Id
                           && log.MovementType == StockMovementType.Adjustment,
                    cancellationToken);

            if (hasAdjustmentLogs)
            {
                throw new BusinessRuleViolationException("Adjustment already applied.");
            }
        }

        var approval = await _dbContext.Approvals
            .FirstOrDefaultAsync(
                a => a.EntityType == EntityTypes.Request && a.EntityId == request.Id,
                cancellationToken);

        if (approval is null)
        {
            throw new NotFoundException("Approval record not found for request.");
        }

        var result = await _approvalService.ApplyDecisionAsync(
            approval.Id,
            actorUserId,
            ApprovalDecision.Approve,
            remarks,
            cancellationToken);

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.RequestManagerApproved,
            EntityTypes.Request,
            request.Id,
            null,
            new { result.Request?.Status, Remarks = remarks });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Action={Action} EntityId={EntityId} ActorUserId={ActorUserId} CorrelationId={CorrelationId}",
            "ManagerDecision",
            request.Id,
            actorUserId,
            GetCorrelationId());

        return result.Request?.Status ?? request.Status;
    }

    private async Task<RequestStatus> RejectAsManager(
        Guid requestId,
        Guid actorUserId,
        string? remarks,
        CancellationToken cancellationToken)
    {
        var request = await _dbContext.Requests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Request not found.");
        }

        if (request.Status != RequestStatus.PendingManager)
        {
            throw new BusinessRuleViolationException("Only pending manager requests can be rejected.");
        }

        var approval = await _dbContext.Approvals
            .FirstOrDefaultAsync(
                a => a.EntityType == EntityTypes.Request && a.EntityId == request.Id,
                cancellationToken);

        if (approval is null)
        {
            throw new NotFoundException("Approval record not found for request.");
        }

        var result = await _approvalService.ApplyDecisionAsync(
            approval.Id,
            actorUserId,
            ApprovalDecision.Reject,
            remarks,
            cancellationToken);

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.RequestManagerRejected,
            EntityTypes.Request,
            request.Id,
            null,
            new { result.Request?.Status, Remarks = remarks });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Action={Action} EntityId={EntityId} ActorUserId={ActorUserId} CorrelationId={CorrelationId}",
            "ManagerDecision",
            request.Id,
            actorUserId,
            GetCorrelationId());

        return result.Request?.Status ?? request.Status;
    }

    public async Task<RequestStatus> IssueRequest(Guid requestId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var request = await _dbContext.Requests
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Request not found.");
        }

        if (request.Status != RequestStatus.Approved)
        {
            throw new BusinessRuleViolationException("Request must be approved before issuing.");
        }

        await _userService.EnsureUserHasRoleAsync(actorUserId, RoleNames.InventoryOfficer, cancellationToken);

        var movementType = request.RequestType switch
        {
            RequestType.Borrow => StockMovementType.Borrow,
            RequestType.AdjustmentDamageLoss => StockMovementType.Adjustment,
            _ => StockMovementType.Out
        };

        var hasIssueLogs = await _dbContext.StockLogs
            .AsNoTracking()
            .AnyAsync(
                log => log.RefType == EntityTypes.Request
                       && log.RefId == request.Id
                       && (request.RequestType == RequestType.Borrow
                           ? log.MovementType == StockMovementType.Borrow
                           : request.RequestType == RequestType.AdjustmentDamageLoss
                               ? log.MovementType == StockMovementType.Adjustment
                               : log.MovementType == StockMovementType.Out),
                cancellationToken);

        if (hasIssueLogs)
        {
            throw new BusinessRuleViolationException("Request already issued.");
        }

        var lineGroups = request.Lines
            .Select(line => new { line.InventoryId, Qty = line.QtyApproved ?? line.QtyRequested })
            .GroupBy(x => x.InventoryId)
            .Select(group => new { InventoryId = group.Key, Qty = group.Sum(x => x.Qty) })
            .ToList();

        foreach (var group in lineGroups)
        {
            if (request.RequestType == RequestType.AdjustmentDamageLoss)
            {
                if (group.Qty == 0)
                {
                    continue;
                }

                await _stockLedgerService.ApplyMovement(
                    movementType,
                    group.InventoryId,
                    group.Qty,
                    EntityTypes.Request,
                    request.Id,
                    actorUserId,
                    cancellationToken);
            }
            else
            {
                if (group.Qty < 0)
                {
                    throw new BusinessRuleViolationException("Issued quantity cannot be negative.");
                }

                if (group.Qty == 0)
                {
                    continue;
                }

                await _stockLedgerService.ApplyMovement(
                    movementType,
                    group.InventoryId,
                    -group.Qty,
                    EntityTypes.Request,
                    request.Id,
                    actorUserId,
                    cancellationToken);
            }
        }

        request.Status = RequestStatus.Issued;
        request.IssuedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        if (request.RequestType == RequestType.MaintenanceIssue
            || request.RequestType == RequestType.AdjustmentDamageLoss)
        {
            request.Status = RequestStatus.Closed;
            request.ClosedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
        }

        if (request.RequestType == RequestType.Borrow)
        {
            var existingLoan = await _dbContext.Loans.AnyAsync(
                loan => loan.RequestId == request.Id,
                cancellationToken);

            if (existingLoan)
            {
                throw new BusinessRuleViolationException("Loan already exists for this request.");
            }

            var loan = new Loan
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                BorrowerUserId = request.RequesterUserId,
                AssetId = request.AssetId,
                Status = LoanStatus.Open,
                IssuedAt = request.IssuedAt ?? DateTime.UtcNow
            };

            var loanLines = request.Lines
                .Select(line => new
                {
                    Line = line,
                    QtyIssued = line.QtyApproved ?? line.QtyRequested
                })
                .Where(x => x.QtyIssued > 0)
                .Select(x => new LoanLine
                {
                    Id = Guid.NewGuid(),
                    LoanId = loan.Id,
                    InventoryId = x.Line.InventoryId,
                    QtyIssued = x.QtyIssued,
                    QtyReturned = 0m
                })
                .ToList();

            _dbContext.Loans.Add(loan);
            _dbContext.LoanLines.AddRange(loanLines);

            request.Status = RequestStatus.Closed;
            request.ClosedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
        }

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.RequestIssued,
            EntityTypes.Request,
            request.Id,
            null,
            new { request.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Action={Action} EntityId={EntityId} ActorUserId={ActorUserId} CorrelationId={CorrelationId}",
            "IssueRequest",
            request.Id,
            actorUserId,
            GetCorrelationId());

        return request.Status;
    }


    public async Task<IssueRequestResult> IssueMaintenanceIssueAsync(
        IssueRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var status = await IssueRequest(command.RequestId, command.ActorUserId, cancellationToken);
        return new IssueRequestResult(command.RequestId, status);
    }
}
