using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NVGInventory.Data;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public sealed class PlanningCspTopsisDemoSeederTests : SqlServerIntegrationTestBase
{
    public PlanningCspTopsisDemoSeederTests(SqlServerIntegrationFixture fixture) : base(fixture) { }

    [SqlServerFact]
    public async Task GoldPath_ProducesOneFeasibleAssignmentWithExplainableTopsisScore()
    {
        await using var db = CreateDbContext();
        await new PlanningCspTopsisDemoSeeder(db, new TestEnvironment(), new BCryptPasswordHashService()).SeedAsync();
        var tripId = await GetTripIdAsync(db, "CSP-DEMO-001-GOLD-PATH");

        var result = await CreateDecisionService(db).GetDecisionSupportAsync(tripId);

        Assert.True(result.ResourcesEvaluated);
        Assert.Equal(1, result.FeasibleCombinationCount);
        var best = Assert.Single(result.Recommendations);
        Assert.Equal("CSP Demo - Juan BestFit", best.DriverName);
        Assert.Equal("CSP-TRK-1001", best.TruckCode);
        Assert.InRange(best.Score, 0m, 1m);
        Assert.Equal(4, best.CriteriaContributions.Count);
        Assert.All(best.CriteriaContributions, contribution => Assert.False(string.IsNullOrWhiteSpace(contribution.Explanation)));
        Assert.Equal(PlanningDecisionSupportService.CriteriaWeightVersion, result.CriteriaWeightVersion);
    }

    [SqlServerFact]
    public async Task MultipleFeasibleAssignments_AreRankedDeterministicallyWithExclusionsExplained()
    {
        await using var db = CreateDbContext();
        await new PlanningCspTopsisDemoSeeder(db, new TestEnvironment(), new BCryptPasswordHashService()).SeedAsync();
        var result = await CreateDecisionService(db).GetDecisionSupportAsync(await GetTripIdAsync(db, "CSP-DEMO-002-EXCLUSIONS"));
        var codes = result.ExcludedResources.SelectMany(resource => resource.Checks).Where(check => check.State == PlanningCheckState.Blocked).Select(check => check.Code).ToHashSet();

        Assert.True(result.ResourcesEvaluated);
        Assert.Equal(3, result.FeasibleCombinationCount);
        var rankedTrucks = result.Recommendations.Select(recommendation => recommendation.TruckCode).ToArray();
        Assert.Equal(new[] { "CSP-TRK-1001", "CSP-TRK-1002", "CSP-TRK-1003" }, rankedTrucks);
        Assert.All(result.Recommendations, recommendation =>
        {
            Assert.Equal("CSP Demo - Juan BestFit", recommendation.DriverName);
            Assert.NotEmpty(recommendation.Reasons);
            Assert.All(recommendation.CriteriaContributions, contribution => Assert.False(string.IsNullOrWhiteSpace(contribution.Explanation)));
        });
        var ranked = result.Recommendations.ToArray();
        Assert.True(ranked[0].Score > ranked[1].Score);
        Assert.True(ranked[1].Score > ranked[2].Score);
        Assert.Contains("DRIVER_OVERLAP", codes);
        Assert.Contains("TRUCK_OVERLAP", codes);
        Assert.Contains("TRUCK_MAINTENANCE", codes);
        Assert.Contains("EQUIPMENT_INCOMPATIBLE", codes);
        Assert.Contains("PICKUP_UNREACHABLE", codes);
    }

    [SqlServerFact]
    public async Task MissingHandoffRequirements_AllowRanking_ButBlockReady()
    {
        await using var db = CreateDbContext();
        await new PlanningCspTopsisDemoSeeder(db, new TestEnvironment(), new BCryptPasswordHashService()).SeedAsync();
        var service = CreateDecisionService(db);

        var atw = await service.GetDecisionSupportAsync(await GetTripIdAsync(db, "CSP-DEMO-003-ATW-HANDOFF-BLOCKER"));
        var container = await service.GetDecisionSupportAsync(await GetTripIdAsync(db, "CSP-DEMO-004-CONTAINER-HANDOFF-BLOCKER"));

        Assert.True(atw.ResourcesEvaluated);
        Assert.NotEmpty(atw.Recommendations);
        Assert.False(atw.CanMarkReady);
        Assert.Contains(atw.BookingChecks, check => check.Code == "MANDATORY_DOCUMENTS" && check.State == PlanningCheckState.Blocked);
        Assert.True(container.ResourcesEvaluated);
        Assert.NotEmpty(container.Recommendations);
        Assert.False(container.CanMarkReady);
        Assert.Contains(container.BookingChecks, check => check.Code == "CONTAINER_NUMBER" && check.State == PlanningCheckState.Blocked);
    }

    [SqlServerFact]
    public async Task PastSchedule_BlocksBeforeResourceEvaluation_WhileAllInvalidWasEvaluated()
    {
        await using var db = CreateDbContext();
        await new PlanningCspTopsisDemoSeeder(db, new TestEnvironment(), new BCryptPasswordHashService()).SeedAsync();
        var service = CreateDecisionService(db);

        var past = await service.GetDecisionSupportAsync(await GetTripIdAsync(db, "CSP-DEMO-005-PAST-SCHEDULE"));
        var allInvalid = await service.GetDecisionSupportAsync(await GetTripIdAsync(db, "CSP-DEMO-006-NO-FEASIBLE-ASSIGNMENT"));

        Assert.False(past.ResourcesEvaluated);
        Assert.Empty(past.ExcludedResources);
        Assert.Empty(past.Recommendations);
        Assert.Contains(past.BookingChecks, check => check.Code == "SCHEDULE_FUTURE" && check.State == PlanningCheckState.Blocked);
        Assert.True(allInvalid.ResourcesEvaluated);
        Assert.Equal(0, allInvalid.FeasibleCombinationCount);
        Assert.NotEmpty(allInvalid.ExcludedResources);
        Assert.Empty(allInvalid.Recommendations);
    }

    [SqlServerFact]
    public async Task SelectedRecommendation_CanMoveGoldPathTripToReadyForDispatch()
    {
        await using var db = CreateDbContext();
        await new PlanningCspTopsisDemoSeeder(db, new TestEnvironment(), new BCryptPasswordHashService()).SeedAsync();
        var tripId = await GetTripIdAsync(db, "CSP-DEMO-001-GOLD-PATH");
        var service = CreateDecisionService(db);
        var recommended = Assert.Single((await service.GetDecisionSupportAsync(tripId)).Recommendations);

        var trip = await db.DispatchTrips.SingleAsync(item => item.Id == tripId);
        trip.DriverUserId = recommended.DriverUserId;
        trip.TruckAssetId = recommended.TruckAssetId;
        await db.SaveChangesAsync();

        var validated = await service.ValidateForDispatchAsync(tripId);
        var result = await service.MarkReadyAsync(
            tripId,
            new MarkTripReadyCommand(
                Convert.ToBase64String(trip.RowVersion),
                validated.RecommendationToken,
                recommended.Rank,
                null),
            DispatcherActor(recommended.DriverUserId));

        Assert.Equal(TripStatus.ReadyForDispatch, result.Status);
        Assert.Equal(1, result.SelectedRank);
        Assert.False(result.WasManualOverride);
    }

    [SqlServerFact]
    public async Task ResourceAvailabilityChange_InvalidatesStaleRecommendation()
    {
        await using var db = CreateDbContext();
        await new PlanningCspTopsisDemoSeeder(db, new TestEnvironment(), new BCryptPasswordHashService()).SeedAsync();
        var tripId = await GetTripIdAsync(db, "CSP-DEMO-007-STALE-RECOMMENDATION");
        var snapshots = new PlanningRecommendationSnapshotStore(new MemoryCache(new MemoryCacheOptions()));
        var notifier = new SnapshotInvalidatingNotifier(snapshots);
        var service = new PlanningDecisionSupportService(db, new NoOpAuditService(), snapshots, notifier);

        var firstRecommendation = (await service.GetDecisionSupportAsync(tripId)).Recommendations.First();
        var trip = await db.DispatchTrips.SingleAsync(item => item.Id == tripId);
        trip.DriverUserId = firstRecommendation.DriverUserId;
        trip.TruckAssetId = firstRecommendation.TruckAssetId;
        await db.SaveChangesAsync();
        var validated = await service.ValidateForDispatchAsync(tripId);

        var driver = await db.DispatchDrivers.SingleAsync(item => item.UserId == firstRecommendation.DriverUserId);
        driver.Status = "Unavailable";
        await db.SaveChangesAsync();
        await notifier.InvalidateAsync("driver-availability-changed", tripId);

        Assert.False(snapshots.TryGet(validated.RecommendationToken, out _));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.MarkReadyAsync(
            tripId,
            new MarkTripReadyCommand(
                Convert.ToBase64String(trip.RowVersion),
                validated.RecommendationToken,
                validated.Recommendations.First().Rank,
                null),
            DispatcherActor(firstRecommendation.DriverUserId)));
    }

    private static async Task<Guid> GetTripIdAsync(InventoryDbContext db, string reference) =>
        await db.DispatchTrips.Where(trip => trip.Notes != null && trip.Notes.Contains(reference)).Select(trip => trip.Id).SingleAsync();

    private static PlanningDecisionSupportService CreateDecisionService(InventoryDbContext db)
    {
        var snapshots = new PlanningRecommendationSnapshotStore(new MemoryCache(new MemoryCacheOptions()));
        return new PlanningDecisionSupportService(db, new NoOpAuditService(), snapshots, new NoOpPlanningNotifier());
    }

    private static DispatchActorContext DispatcherActor(Guid userId) =>
        new(userId, false, true, false, false, false);

    private sealed class NoOpPlanningNotifier : IPlanningAvailabilityNotifier
    {
        public Task InvalidateAsync(string reason, Guid? tripId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SnapshotInvalidatingNotifier : IPlanningAvailabilityNotifier
    {
        private readonly PlanningRecommendationSnapshotStore _snapshots;

        public SnapshotInvalidatingNotifier(PlanningRecommendationSnapshotStore snapshots) => _snapshots = snapshots;

        public Task InvalidateAsync(string reason, Guid? tripId = null, CancellationToken cancellationToken = default)
        {
            _snapshots.InvalidateAll();
            return Task.CompletedTask;
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "NVGInventory.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
