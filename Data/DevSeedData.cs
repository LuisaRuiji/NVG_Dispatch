using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;

namespace NVGInventory.Data;

internal static class DevSeedData
{
    public static async Task EnsureSeededAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        const string seedUsername = "Superadmin";
        const string seedEmail = "Superadmin@nvg.com";
        const string seedPassword = "Super123!";

        var existing = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == seedUsername, cancellationToken);
        if (existing is null)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = seedUsername,
                Email = seedEmail,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword)
            };

            dbContext.Users.Add(user);

            var role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.SuperAdmin, cancellationToken);
            if (role is not null)
            {
                dbContext.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
