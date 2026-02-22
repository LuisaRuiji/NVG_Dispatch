using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Services;

public sealed record RequestListItem(
    Guid Id,
    RequestType RequestType,
    RequestStatus Status,
    Guid RequesterUserId,
    string RequesterUsername,
    Guid? AssetId,
    string? AssetCode,
    string? Purpose,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? IssuedAt,
    DateTime? ClosedAt);

public sealed record RequestLineDetail(
    Guid Id,
    Guid InventoryId,
    string InventoryName,
    string Unit,
    ItemType ItemType,
    decimal CurrentQuantity,
    decimal QtyRequested,
    decimal? QtyApproved,
    string? Remarks);

public sealed record ApprovalSummary(
    Guid ApprovalId,
    ApprovalStatus Status,
    int CurrentStep,
    string? NextApproverRole,
    string WorkflowKey);

public sealed record ApprovalActionSummary(
    Guid Id,
    ApprovalDecision Decision,
    string? Remarks,
    Guid ActorUserId,
    string? ActorUsername,
    DateTime CreatedAt,
    int StepOrder);

public sealed record RequestStockLogSummary(
    Guid Id,
    StockMovementType MovementType,
    decimal Quantity,
    Guid ActorUserId,
    string? ActorUsername,
    DateTime CreatedAt);

public sealed record RequestDetail(
    Guid Id,
    RequestType RequestType,
    RequestStatus Status,
    Guid RequesterUserId,
    string RequesterUsername,
    Guid? AssetId,
    string? AssetCode,
    string? Purpose,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? IssuedAt,
    DateTime? ClosedAt,
    Guid? LoanId,
    IReadOnlyCollection<RequestLineDetail> Lines,
    ApprovalSummary? Approval,
    IReadOnlyCollection<ApprovalActionSummary> ApprovalActions,
    IReadOnlyCollection<RequestStockLogSummary> StockLogs);

public sealed class RequestQueryService
{
    private readonly InventoryDbContext _dbContext;

    public RequestQueryService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedQueryResult<RequestListItem>> GetRequestsAsync(
        RequestType? requestType,
        IReadOnlyCollection<RequestStatus>? statuses,
        Guid? requesterUserId,
        Guid? assetId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Requests
            .AsNoTracking()
            .Include(r => r.Requester)
            .Include(r => r.Asset)
            .AsQueryable();

        if (requestType.HasValue)
        {
            query = query.Where(r => r.RequestType == requestType.Value);
        }

        if (statuses is { Count: > 0 })
        {
            query = query.Where(r => statuses.Contains(r.Status));
        }

        if (requesterUserId.HasValue)
        {
            query = query.Where(r => r.RequesterUserId == requesterUserId.Value);
        }

        if (assetId.HasValue)
        {
            query = query.Where(r => r.AssetId == assetId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var results = await query
            .OrderByDescending(r => r.SubmittedAt ?? r.CreatedAt)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RequestListItem(
                r.Id,
                r.RequestType,
                r.Status,
                r.RequesterUserId,
                r.Requester != null ? r.Requester.Username : string.Empty,
                r.AssetId,
                r.Asset != null ? r.Asset.AssetCode : null,
                r.Purpose,
                r.CreatedAt,
                r.SubmittedAt,
                r.ApprovedAt,
                r.IssuedAt,
                r.ClosedAt))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<RequestListItem>(results, totalCount);
    }

    public async Task<RequestDetail?> GetRequestDetailAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.Requests
            .AsNoTracking()
            .Include(r => r.Requester)
            .Include(r => r.Asset)
            .Include(r => r.Lines)
            .ThenInclude(line => line.InventoryItem)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            return null;
        }

        ApprovalSummary? approvalSummary = null;
        List<ApprovalActionSummary> approvalActions = new();
        var approval = await _dbContext.Approvals
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.EntityType == EntityTypes.Request && a.EntityId == request.Id,
                cancellationToken);

        if (approval is not null)
        {
            string? nextApproverRole = null;
            if (approval.Status == ApprovalStatus.Pending)
            {
                nextApproverRole = await _dbContext.WorkflowSteps
                    .AsNoTracking()
                    .Where(step => step.WorkflowKey == approval.WorkflowKey && step.StepOrder == approval.CurrentStep)
                    .Select(step => step.RequiredRole)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            approvalSummary = new ApprovalSummary(
                approval.Id,
                approval.Status,
                approval.CurrentStep,
                nextApproverRole,
                approval.WorkflowKey);

            approvalActions = await _dbContext.ApprovalActions
                .AsNoTracking()
                .Include(action => action.Actor)
                .Where(action => action.ApprovalId == approval.Id)
                .OrderBy(action => action.ActedAt)
                .Select(action => new ApprovalActionSummary(
                    action.Id,
                    action.Decision,
                    action.Remarks,
                    action.ActorUserId,
                    action.Actor != null ? action.Actor.Username : null,
                    action.ActedAt,
                    action.StepOrder))
                .ToListAsync(cancellationToken);
        }

        var loanId = await _dbContext.Loans
            .AsNoTracking()
            .Where(loan => loan.RequestId == request.Id)
            .Select(loan => (Guid?)loan.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var stockLogs = await _dbContext.StockLogs
            .AsNoTracking()
            .Include(log => log.Actor)
            .Where(log => log.RefType == EntityTypes.Request && log.RefId == request.Id)
            .OrderBy(log => log.CreatedAt)
            .Select(log => new RequestStockLogSummary(
                log.Id,
                log.MovementType,
                log.QtyDelta,
                log.ActorUserId,
                log.Actor != null ? log.Actor.Username : null,
                log.CreatedAt))
            .ToListAsync(cancellationToken);

        var lines = request.Lines
            .Select(line => new RequestLineDetail(
                line.Id,
                line.InventoryId,
                line.InventoryItem?.Name ?? string.Empty,
                line.InventoryItem?.Unit ?? string.Empty,
                line.InventoryItem?.ItemType ?? ItemType.Consumable,
                line.InventoryItem?.Quantity ?? 0m,
                line.QtyRequested,
                line.QtyApproved,
                line.Remarks))
            .ToList();

        return new RequestDetail(
            request.Id,
            request.RequestType,
            request.Status,
            request.RequesterUserId,
            request.Requester?.Username ?? string.Empty,
            request.AssetId,
            request.Asset?.AssetCode,
            request.Purpose,
            request.CreatedAt,
            request.SubmittedAt,
            request.ApprovedAt,
            request.IssuedAt,
            request.ClosedAt,
            loanId,
            lines,
            approvalSummary,
            approvalActions,
            stockLogs);
    }
}
