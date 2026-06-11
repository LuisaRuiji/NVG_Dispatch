using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using Xunit;

namespace NVGInventory.Tests;

public sealed class UserServicePasswordPolicyTests
{
    private const string ValidPassword = "SecurePassword1!";

    [Theory]
    [InlineData("Short1!")]
    [InlineData("lowercasepassword1!")]
    [InlineData("NoNumberPassword!")]
    [InlineData("NoSpecialPassword1")]
    public async Task CreateUserAsync_RejectsPasswordsThatDoNotMeetPolicy(string password)
    {
        using var dbContext = CreateDbContext();
        var service = new UserService(dbContext);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            service.CreateUserAsync(new CreateUserCommand($"user_{Guid.NewGuid():N}", password, null)));

        Assert.Equal(PasswordPolicy.RequirementMessage, ex.Message);
    }

    [Theory]
    [InlineData("Password123456!")]
    [InlineData("P@ssword123456!")]
    [InlineData("Welcome1234567!")]
    [InlineData("ChangeMe123456!")]
    public async Task CreateUserAsync_RejectsCommonPasswordsEvenWhenComplexityPasses(string password)
    {
        using var dbContext = CreateDbContext();
        var service = new UserService(dbContext);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            service.CreateUserAsync(new CreateUserCommand($"user_{Guid.NewGuid():N}", password, null)));

        Assert.Equal(PasswordPolicy.CommonPasswordMessage, ex.Message);
    }

    [Fact]
    public async Task CreateUserAsync_AcceptsPasswordThatMeetsPolicy()
    {
        using var dbContext = CreateDbContext();
        var service = new UserService(dbContext);

        var user = await service.CreateUserAsync(
            new CreateUserCommand($"user_{Guid.NewGuid():N}", ValidPassword, null));

        Assert.True(BCrypt.Net.BCrypt.Verify(ValidPassword, user.PasswordHash));
    }

    [Fact]
    public async Task ResetPasswordAsync_RejectsPasswordThatDoesNotMeetPolicy()
    {
        using var dbContext = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = $"user_{Guid.NewGuid():N}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new UserService(dbContext);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            service.ResetPasswordAsync(user.Id, "NoSpecialPassword1"));

        Assert.Equal(PasswordPolicy.RequirementMessage, ex.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_RejectsCommonPassword()
    {
        using var dbContext = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = $"user_{Guid.NewGuid():N}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new UserService(dbContext);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            service.ResetPasswordAsync(user.Id, "Password123456!"));

        Assert.Equal(PasswordPolicy.CommonPasswordMessage, ex.Message);
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"user-password-policy-{Guid.NewGuid():N}")
            .Options;

        return new InventoryDbContext(options);
    }
}
