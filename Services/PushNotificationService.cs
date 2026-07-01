using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;

namespace NVGInventory.Services;

public interface IPushNotificationService
{
    Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null);

    Task SendToRoleAsync(
        string role,
        string title,
        string body,
        Dictionary<string, string>? data = null);
}

public sealed class PushNotificationService : IPushNotificationService
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        InventoryDbContext dbContext,
        ILogger<PushNotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        _logger.LogInformation(
            "[PUSH] To user {UserId}: {Title} - {Body} | data: {Data}",
            userId,
            title,
            body,
            JsonSerializer.Serialize(data ?? new Dictionary<string, string>()));
        return Task.CompletedTask;
    }

    public async Task SendToRoleAsync(
        string role,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        var userIds = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive)
            .Where(user => user.UserRoles.Any(userRole => userRole.Role != null && userRole.Role.Name == role))
            .Select(user => user.Id)
            .ToListAsync();

        foreach (var userId in userIds)
        {
            await SendToUserAsync(userId, title, body, data);
        }
    }
}
