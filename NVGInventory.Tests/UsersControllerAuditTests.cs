using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NVGInventory.Controllers;
using NVGInventory.Contracts;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Services;
using Xunit;

namespace NVGInventory.Tests;

public sealed class UsersControllerAuditTests
{
    private const string ValidPassword = "SecurePassword1!";
    private static readonly Guid ActorUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task CreateUser_WritesRedactedAuditEntry()
    {
        using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext);

        var result = await controller.CreateUser(
            new CreateUserRequest("new_user", ValidPassword, "new@example.test"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        var audit = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal(AuditActions.UserCreated, audit.Action);
        Assert.Equal(EntityTypes.User, audit.EntityType);
        Assert.Equal(ActorUserId, audit.ActorUserId);
        Assert.Contains("new_user", audit.AfterJson);
        Assert.DoesNotContain(ValidPassword, audit.AfterJson);
        Assert.DoesNotContain("Password", audit.AfterJson);
    }

    [Fact]
    public async Task ResetPassword_WritesAuditEntryWithoutPasswordValue()
    {
        using var dbContext = CreateDbContext();
        var target = new User
        {
            Id = Guid.NewGuid(),
            Username = "target_user",
            Email = "target@example.test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword)
        };
        dbContext.Users.Add(target);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        const string newPassword = "AnotherSecure1!";

        var result = await controller.ResetPassword(
            target.Id,
            new ResetUserPasswordRequest(newPassword),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var audit = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal(AuditActions.UserPasswordReset, audit.Action);
        Assert.Equal(target.Id, audit.EntityId);
        Assert.Contains("passwordReset", audit.AfterJson);
        Assert.DoesNotContain(newPassword, audit.AfterJson);
        Assert.DoesNotContain("PasswordHash", audit.AfterJson);
    }

    private static UsersController CreateController(InventoryDbContext dbContext)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, ActorUserId.ToString()),
                    new Claim(ClaimTypes.Role, RoleNames.SuperAdmin)
                },
                authenticationType: "Test"))
        };

        var auditService = new AuditService(
            dbContext,
            NullLogger<AuditService>.Instance,
            new HttpContextAccessor { HttpContext = httpContext });

        return new UsersController(
            new UserService(dbContext),
            auditService,
            dbContext)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"users-controller-audit-{Guid.NewGuid():N}")
            .Options;

        return new InventoryDbContext(options);
    }
}
