using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Domain.Constants;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Security;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/dispatch/trips/{tripId:guid}/receipt")]
[Authorize(Roles = $"{RoleNames.HeadOfFinance},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
public sealed class TripReceiptController : ControllerBase
{
    private readonly TripReceiptService _service;

    public TripReceiptController(TripReceiptService service)
    {
        _service = service;
    }

    [HttpPost]
    /// <summary>Persists the completed trip's immutable receipt snapshot.</summary>
    public async Task<ActionResult<TripReceiptResponse>> Generate(
        Guid tripId,
        GenerateTripReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.GenerateAsync(
            tripId,
            new GenerateTripReceiptCommand(
                request.ReceiptNumber,
                request.BaseCharge,
                request.AdditionalCharges?.Select(item => new TripReceiptChargeCommand(item.Description, item.Amount)).ToList(),
                request.DiscountAmount,
                request.DiscountPercent,
                request.DiscountReason,
                request.TaxAmount,
                request.PaymentMethod,
                request.PaymentReference,
                request.Notes),
            BuildActor(),
            cancellationToken);

        return Ok(Map(result));
    }

    [HttpGet("history")]
    /// <summary>Returns the immutable receipt and correction ledger for this trip.</summary>
    public async Task<ActionResult<IReadOnlyCollection<TripReceiptResponse>>> GetHistory(
        Guid tripId,
        CancellationToken cancellationToken) =>
        Ok((await _service.GetHistoryAsync(tripId, BuildActor(), cancellationToken)).Select(Map).ToList());

    [HttpPost("{receiptId:guid}/reverse")]
    /// <summary>Creates an auditable reversal rather than altering a previously issued receipt.</summary>
    public async Task<ActionResult<TripReceiptResponse>> Reverse(
        Guid tripId,
        Guid receiptId,
        ReverseTripReceiptRequest request,
        CancellationToken cancellationToken) =>
        Ok(Map(await _service.ReverseAsync(tripId, receiptId, request.Reason, BuildActor(), cancellationToken)));

    private DispatchActorContext BuildActor() => new(
        User.GetUserId(),
        false,
        false,
        false,
        User.IsInRole(RoleNames.HeadOfFinance),
        false,
        User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.SuperAdmin));

    private static TripReceiptResponse Map(TripReceiptResult result) => new(
        result.Id,
        result.TripId,
        result.ReceiptNumber,
        result.GeneratedAt,
        result.Currency,
        result.CustomerName,
        result.ContainerNumber,
        result.BookingNumber,
        result.PickupLocation,
        result.DropoffLocation,
        result.BaseCharge,
        result.AdditionalCharges.Select(item => new TripReceiptChargeResponse(item.Description, item.Amount)).ToList(),
        result.Subtotal,
        result.DiscountAmount,
        result.DiscountPercent,
        result.DiscountReason,
        result.TaxAmount,
        result.Total,
        result.PaymentMethod,
        result.PaymentReference,
        result.Notes,
        result.IsReversal,
        result.ReversesReceiptId,
        result.CorrectionReason,
        result.GeneratedByUserId);
}

public sealed record TripReceiptChargeRequest(string Description, decimal Amount);

public sealed record GenerateTripReceiptRequest(
    string ReceiptNumber,
    decimal? BaseCharge,
    IReadOnlyCollection<TripReceiptChargeRequest>? AdditionalCharges,
    decimal? DiscountAmount,
    decimal? DiscountPercent,
    string? DiscountReason,
    decimal? TaxAmount,
    string? PaymentMethod,
    string? PaymentReference,
    string? Notes);

public sealed record ReverseTripReceiptRequest(string Reason);

public sealed record TripReceiptChargeResponse(string Description, decimal Amount);

public sealed record TripReceiptResponse(
    Guid Id,
    Guid TripId,
    string ReceiptNumber,
    DateTime GeneratedAt,
    string Currency,
    string CustomerName,
    string? ContainerNumber,
    string? BookingNumber,
    string? PickupLocation,
    string? DropoffLocation,
    decimal BaseCharge,
    IReadOnlyCollection<TripReceiptChargeResponse> AdditionalCharges,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal? DiscountPercent,
    string? DiscountReason,
    decimal TaxAmount,
    decimal Total,
    string? PaymentMethod,
    string? PaymentReference,
    string? Notes,
    bool IsReversal,
    Guid? ReversesReceiptId,
    string? CorrectionReason,
    Guid GeneratedByUserId);
