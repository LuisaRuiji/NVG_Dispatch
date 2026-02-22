using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record ReturnLoanLineInput(
    Guid LoanLineId,
    decimal QtyReturnedIncrement,
    ReturnCondition Condition,
    string? MissingComponentsJson);

public sealed class LoanWorkflowService
{
    private readonly InventoryDbContext _dbContext;
    private readonly StockLedgerService _stockLedgerService;
    private readonly UserService _userService;
    private readonly IAuditService? _auditService;
    private readonly ILogger<LoanWorkflowService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LoanWorkflowService(
        InventoryDbContext dbContext,
        StockLedgerService stockLedgerService,
        UserService userService,
        IAuditService? auditService = null,
        ILogger<LoanWorkflowService>? logger = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _dbContext = dbContext;
        _stockLedgerService = stockLedgerService;
        _userService = userService;
        _auditService = auditService;
        _logger = logger ?? NullLogger<LoanWorkflowService>.Instance;
        _httpContextAccessor = httpContextAccessor ?? new HttpContextAccessor();
    }

    private string GetCorrelationId()
    {
        return _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
               ?? _httpContextAccessor.HttpContext?.TraceIdentifier
               ?? "-";
    }

    public async Task<LoanStatus> ReturnLoanAsync(
        Guid loanId,
        Guid actorUserId,
        IReadOnlyCollection<ReturnLoanLineInput> lines,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            throw new BusinessRuleViolationException("Loan return must include at least one line.");
        }

        var loan = await _dbContext.Loans
            .Include(l => l.Lines)
            .FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);

        if (loan is null)
        {
            throw new NotFoundException("Loan not found.");
        }

        if (loan.Status == LoanStatus.Closed)
        {
            throw new BusinessRuleViolationException("Loan is not open for returns.");
        }

        await _userService.EnsureUserHasRoleAsync(actorUserId, RoleNames.InventoryOfficer, cancellationToken);

        var duplicateLine = lines
            .GroupBy(line => line.LoanLineId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateLine is not null)
        {
            throw new BusinessRuleViolationException("Loan return includes duplicate loan line.");
        }

        var loanLinesById = loan.Lines.ToDictionary(line => line.Id, line => line);

        foreach (var line in lines)
        {
            if (!loanLinesById.TryGetValue(line.LoanLineId, out var loanLine))
            {
                throw new BusinessRuleViolationException("Loan return includes invalid loan line.");
            }

            if (line.QtyReturnedIncrement <= 0)
            {
                throw new BusinessRuleViolationException("Returned quantity must be greater than zero.");
            }

            if (line.QtyReturnedIncrement % 1m != 0m)
            {
                throw new BusinessRuleViolationException("Returned quantity must be a whole number.");
            }

            var remaining = loanLine.QtyIssued - loanLine.QtyReturned;
            if (remaining <= 0)
            {
                throw new BusinessRuleViolationException("All issued quantity already returned.");
            }

            if (line.QtyReturnedIncrement > remaining)
            {
                throw new BusinessRuleViolationException("Returned quantity exceeds issued quantity.");
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var line in lines)
        {
            var loanLine = loanLinesById[line.LoanLineId];
            loanLine.QtyReturned += line.QtyReturnedIncrement;

            var returnEntry = new LoanLineReturn
            {
                Id = Guid.NewGuid(),
                LoanLineId = loanLine.Id,
                QtyReturned = line.QtyReturnedIncrement,
                Condition = line.Condition,
                MissingComponentsJson = line.MissingComponentsJson,
                ReceivedByUserId = actorUserId,
                ReturnedAt = now
            };

            _dbContext.LoanLineReturns.Add(returnEntry);

            _auditService?.AddEntry(
                actorUserId,
                AuditActions.LoanReturn,
                EntityTypes.LoanReturn,
                returnEntry.Id,
                null,
                new
                {
                    LoanId = loan.Id,
                    LoanLineId = loanLine.Id,
                    QtyReturned = line.QtyReturnedIncrement,
                    Condition = line.Condition.ToString()
                });

            if (line.Condition == ReturnCondition.Good)
            {
                await _stockLedgerService.ApplyMovement(
                    StockMovementType.Return,
                    loanLine.InventoryId,
                    line.QtyReturnedIncrement,
                    EntityTypes.Loan,
                    loan.Id,
                    actorUserId,
                    cancellationToken);
            }
            else if (line.Condition == ReturnCondition.Damaged || line.Condition == ReturnCondition.Lost)
            {
                await _stockLedgerService.ApplyMovement(
                    StockMovementType.WriteOff,
                    loanLine.InventoryId,
                    -line.QtyReturnedIncrement,
                    EntityTypes.Loan,
                    loan.Id,
                    actorUserId,
                    cancellationToken);
            }
        }

        var allReturned = loan.Lines.All(line => line.QtyReturned >= line.QtyIssued);
        loan.Status = allReturned ? LoanStatus.Closed : LoanStatus.PartiallyReturned;
        if (allReturned)
        {
            loan.ClosedAt = now;
        }

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.LoanReturn,
            EntityTypes.Loan,
            loan.Id,
            null,
            new { loan.Status, LineCount = lines.Count });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Action={Action} EntityId={EntityId} ActorUserId={ActorUserId} CorrelationId={CorrelationId}",
            "ReturnLoanItems",
            loan.Id,
            actorUserId,
            GetCorrelationId());

        return loan.Status;
    }
}
