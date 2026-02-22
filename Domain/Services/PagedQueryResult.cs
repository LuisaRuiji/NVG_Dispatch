namespace NVGInventory.Domain.Services;

public sealed record PagedQueryResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount);
