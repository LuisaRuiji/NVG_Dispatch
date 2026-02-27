namespace NVGInventory.Modules.Dispatching.Services;

public sealed record DispatchActorContext(
    Guid UserId,
    bool IsManager,
    bool IsDispatcher,
    bool IsDriver,
    bool IsFinance,
    bool IsCeo)
{
    public bool IsPrivileged => IsManager || IsDispatcher || IsFinance || IsCeo;
}
