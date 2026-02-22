using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Services;

public sealed record LoanListItem(
    Guid Id,
    Guid RequestId,
    LoanStatus Status,
    Guid BorrowerUserId,
    string BorrowerUsername,
    Guid? AssetId,
    string? AssetCode,
    DateTime IssuedAt,
    DateTime? DueAt,
    DateTime? ClosedAt);

public sealed record LoanLineReturnDetail(
    Guid Id,
    decimal QtyReturned,
    ReturnCondition Condition,
    string? MissingComponentsJson,
    Guid ReceivedByUserId,
    string? ReceivedByUsername,
    DateTime ReturnedAt);

public sealed record LoanLineDetail(
    Guid Id,
    Guid InventoryId,
    string InventoryName,
    string Unit,
    ItemType ItemType,
    decimal QtyIssued,
    decimal QtyReturned,
    IReadOnlyCollection<LoanLineReturnDetail> Returns);

public sealed record StockLogSummary(
    Guid Id,
    StockMovementType MovementType,
    decimal Quantity,
    Guid ActorUserId,
    string? ActorUsername,
    DateTime CreatedAt);

public sealed record LoanDetail(
    Guid Id,
    Guid RequestId,
    LoanStatus Status,
    Guid BorrowerUserId,
    string BorrowerUsername,
    Guid? AssetId,
    string? AssetCode,
    DateTime IssuedAt,
    DateTime? DueAt,
    DateTime? ClosedAt,
    IReadOnlyCollection<LoanLineDetail> Lines,
    IReadOnlyCollection<StockLogSummary> StockLogs);

public sealed class LoanQueryService
{
    private readonly InventoryDbContext _dbContext;

    public LoanQueryService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedQueryResult<LoanListItem>> GetLoansAsync(
        IReadOnlyCollection<LoanStatus>? statuses,
        Guid? borrowerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Loans
            .AsNoTracking()
            .Include(l => l.Borrower)
            .Include(l => l.Asset)
            .AsQueryable();

        if (statuses is { Count: > 0 })
        {
            query = query.Where(l => statuses.Contains(l.Status));
        }

        if (borrowerUserId.HasValue)
        {
            query = query.Where(l => l.BorrowerUserId == borrowerUserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var results = await query
            .OrderByDescending(l => l.IssuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LoanListItem(
                l.Id,
                l.RequestId,
                l.Status,
                l.BorrowerUserId,
                l.Borrower != null ? l.Borrower.Username : string.Empty,
                l.AssetId,
                l.Asset != null ? l.Asset.AssetCode : null,
                l.IssuedAt,
                l.DueAt,
                l.ClosedAt))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<LoanListItem>(results, totalCount);
    }

    public async Task<LoanDetail?> GetLoanDetailAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        var loan = await _dbContext.Loans
            .AsNoTracking()
            .Include(l => l.Borrower)
            .Include(l => l.Asset)
            .Include(l => l.Lines)
            .ThenInclude(line => line.InventoryItem)
            .Include(l => l.Lines)
            .ThenInclude(line => line.Returns)
            .ThenInclude(ret => ret.ReceivedBy)
            .FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);

        if (loan is null)
        {
            return null;
        }

        var lines = loan.Lines
            .Select(line => new LoanLineDetail(
                line.Id,
                line.InventoryId,
                line.InventoryItem?.Name ?? string.Empty,
                line.InventoryItem?.Unit ?? string.Empty,
                line.InventoryItem?.ItemType ?? ItemType.Consumable,
                line.QtyIssued,
                line.QtyReturned,
                line.Returns
                    .OrderBy(ret => ret.ReturnedAt)
                    .Select(ret => new LoanLineReturnDetail(
                        ret.Id,
                        ret.QtyReturned,
                        ret.Condition,
                        ret.MissingComponentsJson,
                        ret.ReceivedByUserId,
                        ret.ReceivedBy?.Username,
                        ret.ReturnedAt))
                    .ToList()))
            .ToList();

        var stockLogs = await _dbContext.StockLogs
            .AsNoTracking()
            .Include(log => log.Actor)
            .Where(log => log.RefType == EntityTypes.Loan && log.RefId == loan.Id)
            .OrderByDescending(log => log.CreatedAt)
            .Select(log => new StockLogSummary(
                log.Id,
                log.MovementType,
                log.QtyDelta,
                log.ActorUserId,
                log.Actor != null ? log.Actor.Username : null,
                log.CreatedAt))
            .ToListAsync(cancellationToken);

        return new LoanDetail(
            loan.Id,
            loan.RequestId,
            loan.Status,
            loan.BorrowerUserId,
            loan.Borrower?.Username ?? string.Empty,
            loan.AssetId,
            loan.Asset?.AssetCode,
            loan.IssuedAt,
            loan.DueAt,
            loan.ClosedAt,
            lines,
            stockLogs);
    }
}
