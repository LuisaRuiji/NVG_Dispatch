using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Security;

public sealed record SensitiveFieldRotationResult(int TripsScanned, int TripsUpdated);

public sealed class SensitiveFieldRotationService
{
    private readonly InventoryDbContext _dbContext;

    public SensitiveFieldRotationService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SensitiveFieldRotationResult> RotateDispatchFinancialFieldsAsync(
        CancellationToken cancellationToken = default)
    {
        SensitiveFieldProtector.RequireConfigured();

        var trips = await _dbContext.DispatchTrips
            .Where(trip =>
                trip.Rate != null ||
                trip.Payroll != null ||
                trip.Allowance != null ||
                trip.FuelAmount != null ||
                trip.FuelPricePerLiter != null ||
                trip.OfficialReceiptNumber != null)
            .ToListAsync(cancellationToken);

        foreach (var trip in trips)
        {
            MarkFinancialFieldsModified(trip);
        }

        if (trips.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new SensitiveFieldRotationResult(trips.Count, trips.Count);
    }

    private void MarkFinancialFieldsModified(Trip trip)
    {
        var entry = _dbContext.Entry(trip);
        entry.Property(item => item.Rate).IsModified = true;
        entry.Property(item => item.Payroll).IsModified = true;
        entry.Property(item => item.Allowance).IsModified = true;
        entry.Property(item => item.FuelAmount).IsModified = true;
        entry.Property(item => item.FuelPricePerLiter).IsModified = true;
        entry.Property(item => item.OfficialReceiptNumber).IsModified = true;
    }
}
