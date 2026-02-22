using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;
using NVGInventory.Security;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public sealed class LoansController : ControllerBase
{
    private readonly LoanWorkflowService _loanWorkflowService;
    private readonly LoanQueryService _loanQueryService;

    public LoansController(LoanWorkflowService loanWorkflowService, LoanQueryService loanQueryService)
    {
        _loanWorkflowService = loanWorkflowService;
        _loanQueryService = loanQueryService;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
    public async Task<ActionResult<PagedResult<LoanListItemResponse>>> GetLoans(
        [FromQuery] string[]? status,
        [FromQuery] Guid? borrowerUserId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var rawStatuses = new List<string>();
        if (status is { Length: > 0 })
        {
            foreach (var entry in status)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                rawStatuses.AddRange(entry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        List<LoanStatus>? loanStatuses = null;
        if (rawStatuses.Count > 0)
        {
            loanStatuses = new List<LoanStatus>();
            foreach (var rawStatus in rawStatuses)
            {
                if (!QueryParsing.TryParseEnum(rawStatus, out LoanStatus parsedStatus))
                {
                    return BadRequest("Invalid loan status.");
                }

                if (!loanStatuses.Contains(parsedStatus))
                {
                    loanStatuses.Add(parsedStatus);
                }
            }
        }

        var resolvedPage = page.GetValueOrDefault(1);
        if (resolvedPage < 1)
        {
            return BadRequest("Page must be at least 1.");
        }

        var resolvedPageSize = pageSize.GetValueOrDefault(20);
        if (resolvedPageSize < 1 || resolvedPageSize > 100)
        {
            return BadRequest("Page size must be between 1 and 100.");
        }

        var results = await _loanQueryService.GetLoansAsync(
            loanStatuses,
            borrowerUserId,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);

        var responseItems = results.Items
            .Select(item => new LoanListItemResponse(
                item.Id,
                item.RequestId,
                item.Status,
                item.BorrowerUserId,
                item.BorrowerUsername,
                item.AssetId,
                item.AssetCode,
                item.IssuedAt,
                item.DueAt,
                item.ClosedAt))
            .ToList();

        var response = new PagedResult<LoanListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize);

        return Ok(response);
    }

    [HttpGet("{loanId:guid}")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
    public async Task<ActionResult<LoanDetailResponse>> GetLoan(
        Guid loanId,
        CancellationToken cancellationToken)
    {
        var detail = await _loanQueryService.GetLoanDetailAsync(loanId, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var history = detail.Lines
            .SelectMany(line => line.Returns.Select(ret => new HistoryEntryResponse(
                "RETURN",
                ret.ReceivedByUsername ?? ret.ReceivedByUserId.ToString(),
                null,
                null,
                null,
                ret.QtyReturned,
                ret.Condition.ToString().ToUpperInvariant(),
                ret.ReturnedAt)))
            .OrderBy(entry => entry.Timestamp)
            .ToList();

        var response = new LoanDetailResponse(
            detail.Id,
            detail.RequestId,
            detail.Status,
            detail.BorrowerUserId,
            detail.BorrowerUsername,
            detail.AssetId,
            detail.AssetCode,
            detail.IssuedAt,
            detail.DueAt,
            detail.ClosedAt,
            detail.Lines.Select(line => new LoanLineDetailResponse(
                line.Id,
                line.InventoryId,
                line.InventoryName,
                line.Unit,
                line.ItemType,
                line.QtyIssued,
                line.QtyReturned,
                line.Returns.Select(ret => new LoanLineReturnResponse(
                    ret.Id,
                    ret.QtyReturned,
                    ret.Condition,
                    ret.MissingComponentsJson,
                    ret.ReceivedByUserId,
                    ret.ReceivedByUsername,
                    ret.ReturnedAt)).ToList())).ToList(),
            detail.StockLogs.Select(log => new StockLogSummaryResponse(
                log.Id,
                log.MovementType,
                log.Quantity,
                log.ActorUserId,
                log.ActorUsername,
                log.CreatedAt)).ToList(),
            history);

        return Ok(response);
    }

    [HttpPost("{loanId:guid}/return")]
    [Authorize(Roles = RoleNames.InventoryOfficer)]
    public async Task<ActionResult<LoanReturnResponse>> ReturnLoan(
        Guid loanId,
        LoanReturnRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var status = await _loanWorkflowService.ReturnLoanAsync(
            loanId,
            actorUserId,
            request.Lines.Select(line => new ReturnLoanLineInput(
                line.LoanLineId,
                line.QtyReturnedIncrement,
                line.Condition,
                line.MissingComponentsJson)).ToList(),
            cancellationToken);

        return Ok(new LoanReturnResponse(loanId, status));
    }
}
