# Dispatch Trip Query Builder

This folder centralizes dispatch trip query composition so list and monitoring endpoints never diverge.

## Purpose

- Build `IQueryable<Trip>` pipelines for dispatch list and monitoring endpoints.
- Keep filtering logic consistent across:
  - `/api/dispatch/trips`
  - `/api/dispatch/trips/active`
  - `/api/dispatch/trips/on-hold`
  - `/api/dispatch/trips/failed-attempts`
  - `/api/dispatch/trips/pod-pending`

## Responsibilities

- **Only build queries.**
- **Do not execute queries.**
- **Do not paginate.**
- **Do not perform projections.**

Execution, projection, and pagination remain in `DispatchTripQueryService`.

## Pod Status Rules

`DispatchPodStatusFilter.Pending` must mirror the close guard:

- **Strict mode (`DocVerificationEnabled = true`):** pending when active POD is not verified.
- **Relaxed mode (`DocVerificationEnabled = false`):** pending when active POD is **not uploaded/verified** and no `PodPending` override.

This ensures list filtering and monitoring queries match close eligibility.

## Common Usage

```csharp
var query = _builder.Base(actor);
query = _builder.FilterStatus(query, status);
query = _builder.FilterTruck(query, truckId);
query = _builder.FilterPickupRange(query, from, to);
query = _builder.FilterPodStatus(query, podStatus);

return await ExecuteTripListQueryAsync(query, requiredTypes, page, pageSize, ct);
```

## Adding New Filters

1. Add a new method to `DispatchTripQueryBuilder`.
2. Use it in **all** relevant endpoints.
3. Add or update tests:
   - Query service integration tests
   - Query builder unit tests (InMemory)
