using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Security;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;

    public NotificationsController(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("push-token")]
    public async Task<IActionResult> UpsertPushToken(
        PushTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest("Token is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Platform))
        {
            return BadRequest("Platform is required.");
        }

        var userId = User.GetUserId();
        var platform = request.Platform.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var existing = await _dbContext.PushNotificationTokens
            .FirstOrDefaultAsync(
                token => token.UserId == userId && token.Platform == platform,
                cancellationToken);

        if (existing is null)
        {
            _dbContext.PushNotificationTokens.Add(new PushNotificationToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = request.Token.Trim(),
                Platform = platform,
                RegisteredAt = now,
                LastSeenAt = now
            });
        }
        else
        {
            existing.Token = request.Token.Trim();
            existing.LastSeenAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record PushTokenRequest(string Token, string Platform);
