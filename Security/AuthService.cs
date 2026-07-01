using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;

namespace NVGInventory.Security;

public sealed record LoginCommand(string Username, string Password);

public sealed record AuthResult(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> Roles,
    string AccessToken,
    DateTime ExpiresAtUtc,
    string TokenJti,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    bool RefreshTokenIsPersistent,
    bool MustChangePassword);

public sealed record MfaChallengeResult(
    Guid ChallengeId,
    string Method,
    DateTime ExpiresAtUtc);

public sealed record MfaVerificationAttemptResult(
    bool Success,
    AuthResult? Result,
    string? FailureReason,
    Guid? UserId,
    string? Username,
    IReadOnlyCollection<string> RolesSnapshot);

public sealed record StepUpTokenResult(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> Roles,
    string AccessToken,
    DateTime ExpiresAtUtc,
    string TokenJti);

public sealed record StepUpAttemptResult(
    bool Success,
    StepUpTokenResult? Result,
    string? FailureReason,
    Guid? UserId,
    string? Username,
    IReadOnlyCollection<string> RolesSnapshot);

public sealed record MfaEnrollmentResult(string SecretKey, string OtpAuthUri);

public sealed record MfaStatusResult(
    bool Enabled,
    DateTime? EnabledAt,
    DateTime? LastVerifiedAt);

public sealed record MfaConfigurationAttemptResult(
    bool Success,
    string? FailureReason,
    MfaStatusResult? Status);

public sealed record RefreshTokenAttemptResult(
    bool Success,
    AuthResult? Result,
    string? FailureReason);

public sealed record AuthAttemptResult(
    bool Success,
    AuthResult? Result,
    string? FailureReason,
    Guid? UserId,
    string? Username,
    IReadOnlyCollection<string> RolesSnapshot,
    bool RequiresMfa = false,
    MfaChallengeResult? MfaChallenge = null);

public sealed class AuthService
{
    private const string TotpMethod = "TOTP";

    private readonly InventoryDbContext _dbContext;
    private readonly JwtTokenService _tokenService;
    private readonly JwtOptions _options;
    private readonly TotpAuthenticator _totpAuthenticator;
    private readonly MfaOptions _mfaOptions;
    private readonly IPasswordHashService _passwordHashService;

    public AuthService(
        InventoryDbContext dbContext,
        JwtTokenService tokenService,
        IOptions<JwtOptions> options)
        : this(
            dbContext,
            tokenService,
            options,
            new TotpAuthenticator(),
            Options.Create(new MfaOptions()),
            new BCryptPasswordHashService())
    {
    }

    public AuthService(
        InventoryDbContext dbContext,
        JwtTokenService tokenService,
        IOptions<JwtOptions> options,
        TotpAuthenticator totpAuthenticator,
        IOptions<MfaOptions> mfaOptions,
        IPasswordHashService? passwordHashService = null)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _options = options.Value;
        _totpAuthenticator = totpAuthenticator;
        _mfaOptions = mfaOptions.Value;
        _passwordHashService = passwordHashService ?? new BCryptPasswordHashService();
    }

    public async Task<AuthAttemptResult> TryLoginAsync(
        LoginCommand command,
        string? ipAddress = null,
        string? userAgent = null,
        bool rememberMe = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrWhiteSpace(command.Password))
        {
            return new AuthAttemptResult(false, null, AuthFailureReasons.ValidationError, null, null, Array.Empty<string>());
        }

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                u => u.Username == command.Username,
                cancellationToken);

        if (user is null)
        {
            return new AuthAttemptResult(false, null, AuthFailureReasons.UserNotFound, null, command.Username.Trim(), Array.Empty<string>());
        }

        if (!user.IsActive)
        {
            return new AuthAttemptResult(
                false,
                null,
                AuthFailureReasons.UserInactive,
                user.Id,
                user.Username,
                user.UserRoles
                    .Select(ur => ur.Role?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .Distinct()
                    .ToList());
        }

        var validPassword = _passwordHashService.VerifyPassword(command.Password, user.PasswordHash);
        if (!validPassword)
        {
            return new AuthAttemptResult(
                false,
                null,
                AuthFailureReasons.InvalidCredentials,
                user.Id,
                user.Username,
                user.UserRoles
                    .Select(ur => ur.Role?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .Distinct()
                    .ToList());
        }

        var roles = GetRoleSnapshot(user);

        if (user.MfaEnabled)
        {
            if (string.IsNullOrWhiteSpace(user.MfaSecretKey))
            {
                return new AuthAttemptResult(
                    false,
                    null,
                    AuthFailureReasons.MfaNotEnabled,
                    user.Id,
                    user.Username,
                    roles);
            }

            var challenge = CreateMfaChallenge(
                user.Id,
                ipAddress,
                userAgent);
            _dbContext.MfaChallenges.Add(challenge);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new AuthAttemptResult(
                false,
                null,
                AuthFailureReasons.MfaRequired,
                user.Id,
                user.Username,
                roles,
                RequiresMfa: true,
                MfaChallenge: new MfaChallengeResult(challenge.Id, challenge.Method, challenge.ExpiresAt));
        }

        var token = _tokenService.CreateAccessToken(user, roles);
        var now = DateTime.UtcNow;
        var refreshExpiresAt = GetRefreshTokenExpiresAt(now, rememberMe);
        var refreshToken = CreateRefreshToken(
            user.Id,
            Guid.NewGuid(),
            ipAddress,
            userAgent,
            refreshExpiresAt);
        _dbContext.RefreshTokens.Add(refreshToken.Entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new AuthResult(
            user.Id,
            user.Username,
            roles,
            token.AccessToken,
            token.ExpiresAtUtc,
            token.Jti,
            refreshToken.RawToken,
            refreshToken.Entity.ExpiresAt,
            rememberMe,
            user.MustChangePassword);

        return new AuthAttemptResult(
            true,
            result,
            null,
            result.UserId,
            result.Username,
            result.Roles);
    }

    public async Task<MfaVerificationAttemptResult> TryVerifyMfaLoginAsync(
        Guid challengeId,
        string code,
        string? ipAddress,
        string? userAgent,
        bool rememberMe = false,
        CancellationToken cancellationToken = default)
    {
        if (challengeId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return new MfaVerificationAttemptResult(
                false,
                null,
                AuthFailureReasons.ValidationError,
                null,
                null,
                Array.Empty<string>());
        }

        var challenge = await _dbContext.MfaChallenges
            .Include(mfaChallenge => mfaChallenge.User)
            .ThenInclude(user => user!.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(mfaChallenge => mfaChallenge.Id == challengeId, cancellationToken);

        if (challenge is null)
        {
            return new MfaVerificationAttemptResult(
                false,
                null,
                AuthFailureReasons.InvalidMfaCode,
                null,
                null,
                Array.Empty<string>());
        }

        var user = challenge.User;
        var roles = user is null ? Array.Empty<string>() : GetRoleSnapshot(user);
        var now = DateTime.UtcNow;

        if (challenge.ConsumedAt.HasValue)
        {
            return new MfaVerificationAttemptResult(
                false,
                null,
                AuthFailureReasons.InvalidMfaCode,
                user?.Id,
                user?.Username,
                roles);
        }

        if (challenge.ExpiresAt <= now)
        {
            return new MfaVerificationAttemptResult(
                false,
                null,
                AuthFailureReasons.MfaChallengeExpired,
                user?.Id,
                user?.Username,
                roles);
        }

        if (user is null || !user.IsActive)
        {
            challenge.ConsumedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new MfaVerificationAttemptResult(
                false,
                null,
                AuthFailureReasons.UserInactive,
                user?.Id,
                user?.Username,
                roles);
        }

        var mfaSecret = UnprotectMfaSecret(user.MfaSecretKey);
        if (!user.MfaEnabled || string.IsNullOrWhiteSpace(mfaSecret))
        {
            challenge.ConsumedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new MfaVerificationAttemptResult(
                false,
                null,
                AuthFailureReasons.MfaNotEnabled,
                user.Id,
                user.Username,
                roles);
        }

        if (!VerifyTotp(mfaSecret, code, now))
        {
            return new MfaVerificationAttemptResult(
                false,
                null,
                AuthFailureReasons.InvalidMfaCode,
                user.Id,
                user.Username,
                roles);
        }

        challenge.ConsumedAt = now;
        user.MfaLastVerifiedAt = now;
        await ConsumeActiveChallengesAsync(user.Id, challenge.Id, now, cancellationToken);

        var accessToken = _tokenService.CreateAccessToken(user, roles, now);
        var refreshToken = CreateRefreshToken(
            user.Id,
            Guid.NewGuid(),
            ipAddress,
            userAgent,
            GetRefreshTokenExpiresAt(now, rememberMe));
        _dbContext.RefreshTokens.Add(refreshToken.Entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MfaVerificationAttemptResult(
            true,
            new AuthResult(
                user.Id,
                user.Username,
                roles,
                accessToken.AccessToken,
                accessToken.ExpiresAtUtc,
                accessToken.Jti,
                refreshToken.RawToken,
                refreshToken.Entity.ExpiresAt,
                rememberMe,
                user.MustChangePassword),
            null,
            user.Id,
            user.Username,
            roles);
    }

    public async Task<StepUpAttemptResult> TryStepUpAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return new StepUpAttemptResult(
                false,
                null,
                AuthFailureReasons.ValidationError,
                null,
                null,
                Array.Empty<string>());
        }

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return new StepUpAttemptResult(
                false,
                null,
                AuthFailureReasons.UserNotFound,
                null,
                null,
                Array.Empty<string>());
        }

        var roles = GetRoleSnapshot(user);
        if (!user.IsActive)
        {
            return new StepUpAttemptResult(
                false,
                null,
                AuthFailureReasons.UserInactive,
                user.Id,
                user.Username,
                roles);
        }

        var mfaSecret = UnprotectMfaSecret(user.MfaSecretKey);
        if (!user.MfaEnabled || string.IsNullOrWhiteSpace(mfaSecret))
        {
            return new StepUpAttemptResult(
                false,
                null,
                AuthFailureReasons.MfaNotEnabled,
                user.Id,
                user.Username,
                roles);
        }

        var now = DateTime.UtcNow;
        if (!VerifyTotp(mfaSecret, code, now))
        {
            return new StepUpAttemptResult(
                false,
                null,
                AuthFailureReasons.InvalidMfaCode,
                user.Id,
                user.Username,
                roles);
        }

        user.MfaLastVerifiedAt = now;
        var accessToken = _tokenService.CreateAccessToken(user, roles, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StepUpAttemptResult(
            true,
            new StepUpTokenResult(
                user.Id,
                user.Username,
                roles,
                accessToken.AccessToken,
                accessToken.ExpiresAtUtc,
                accessToken.Jti),
            null,
            user.Id,
            user.Username,
            roles);
    }

    public async Task<MfaStatusResult?> GetMfaStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is null
            ? null
            : new MfaStatusResult(user.MfaEnabled, user.MfaEnabledAt, user.MfaLastVerifiedAt);
    }

    public async Task<MfaEnrollmentResult?> BeginMfaEnrollmentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var secretKey = _totpAuthenticator.GenerateSecretKey();
        user.PendingMfaSecretKey = ProtectMfaSecret(secretKey);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var accountName = string.IsNullOrWhiteSpace(user.Email) ? user.Username : user.Email!;
        var otpAuthUri = _totpAuthenticator.BuildOtpAuthUri(_mfaOptions.Issuer, accountName, secretKey);
        return new MfaEnrollmentResult(secretKey, otpAuthUri);
    }

    public async Task<MfaConfigurationAttemptResult> ConfirmMfaEnrollmentAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return new MfaConfigurationAttemptResult(false, AuthFailureReasons.ValidationError, null);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return new MfaConfigurationAttemptResult(false, AuthFailureReasons.UserInactive, null);
        }

        var pendingSecret = UnprotectMfaSecret(user.PendingMfaSecretKey);
        if (string.IsNullOrWhiteSpace(pendingSecret))
        {
            return new MfaConfigurationAttemptResult(false, AuthFailureReasons.ValidationError, null);
        }

        var now = DateTime.UtcNow;
        if (!VerifyTotp(pendingSecret, code, now))
        {
            return new MfaConfigurationAttemptResult(false, AuthFailureReasons.InvalidMfaCode, null);
        }

        user.MfaEnabled = true;
        user.MfaSecretKey = user.PendingMfaSecretKey;
        user.PendingMfaSecretKey = null;
        user.MfaEnabledAt = now;
        user.MfaLastVerifiedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MfaConfigurationAttemptResult(
            true,
            null,
            new MfaStatusResult(user.MfaEnabled, user.MfaEnabledAt, user.MfaLastVerifiedAt));
    }

    public async Task<MfaConfigurationAttemptResult> DisableMfaAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return new MfaConfigurationAttemptResult(false, AuthFailureReasons.ValidationError, null);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return new MfaConfigurationAttemptResult(false, AuthFailureReasons.UserInactive, null);
        }

        var mfaSecret = UnprotectMfaSecret(user.MfaSecretKey);
        if (!user.MfaEnabled || string.IsNullOrWhiteSpace(mfaSecret))
        {
            return new MfaConfigurationAttemptResult(false, AuthFailureReasons.MfaNotEnabled, null);
        }

        var now = DateTime.UtcNow;
        if (!VerifyTotp(mfaSecret, code, now))
        {
            return new MfaConfigurationAttemptResult(false, AuthFailureReasons.InvalidMfaCode, null);
        }

        user.MfaEnabled = false;
        user.MfaSecretKey = null;
        user.PendingMfaSecretKey = null;
        user.MfaEnabledAt = null;
        user.MfaLastVerifiedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MfaConfigurationAttemptResult(
            true,
            null,
            new MfaStatusResult(user.MfaEnabled, user.MfaEnabledAt, user.MfaLastVerifiedAt));
    }

    public async Task<RefreshTokenAttemptResult> TryRefreshAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return new RefreshTokenAttemptResult(false, null, AuthFailureReasons.ValidationError);
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var existing = await _dbContext.RefreshTokens
            .Include(token => token.User)
            .ThenInclude(user => user!.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (existing is null)
        {
            return new RefreshTokenAttemptResult(false, null, AuthFailureReasons.InvalidCredentials);
        }

        var now = DateTime.UtcNow;
        if (existing.RevokedAt.HasValue)
        {
            await RevokeTokenFamilyAsync(existing.FamilyId, "REUSE_DETECTED", ipAddress, cancellationToken);
            return new RefreshTokenAttemptResult(false, null, AuthFailureReasons.InvalidCredentials);
        }

        if (existing.ExpiresAt <= now)
        {
            existing.RevokedAt = now;
            existing.RevokedByIp = ipAddress;
            existing.RevokedReason = "EXPIRED";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new RefreshTokenAttemptResult(false, null, AuthFailureReasons.InvalidCredentials);
        }

        var user = existing.User;
        if (user is null || !user.IsActive)
        {
            await RevokeTokenFamilyAsync(existing.FamilyId, "USER_INACTIVE", ipAddress, cancellationToken);
            return new RefreshTokenAttemptResult(false, null, AuthFailureReasons.UserInactive);
        }

        var roles = GetRoleSnapshot(user);

        var accessToken = _tokenService.CreateAccessToken(user, roles);
        var familyCreatedAt = await _dbContext.RefreshTokens
            .Where(token => token.FamilyId == existing.FamilyId)
            .MinAsync(token => token.CreatedAt, cancellationToken);
        var isPersistent = existing.ExpiresAt - familyCreatedAt >= TimeSpan.FromDays(1);
        var nextRefreshToken = CreateRefreshToken(
            user.Id,
            existing.FamilyId,
            ipAddress,
            userAgent,
            existing.ExpiresAt);

        existing.RevokedAt = now;
        existing.RevokedByIp = ipAddress;
        existing.RevokedReason = "ROTATED";
        existing.ReplacedByTokenId = nextRefreshToken.Entity.Id;
        _dbContext.RefreshTokens.Add(nextRefreshToken.Entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshTokenAttemptResult(
            true,
            new AuthResult(
                user.Id,
                user.Username,
                roles,
                accessToken.AccessToken,
                accessToken.ExpiresAtUtc,
                accessToken.Jti,
                nextRefreshToken.RawToken,
                nextRefreshToken.Entity.ExpiresAt,
                isPersistent,
                user.MustChangePassword),
            null);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        string newPassword,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new BusinessRuleViolationException("New password is required.");
        }

        PasswordPolicy.EnsureValid(newPassword);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        if (!user.IsActive)
        {
            throw new BusinessRuleViolationException("User must be active.");
        }

        user.PasswordHash = _passwordHashService.HashPassword(newPassword);
        user.MustChangePassword = false;

        var now = DateTime.UtcNow;
        var refreshTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in refreshTokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = ipAddress;
            token.RevokedReason = "PASSWORD_CHANGED";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(
        Guid userId,
        string? refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var existing = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash && token.UserId == userId,
                cancellationToken);

        if (existing is null || existing.RevokedAt.HasValue)
        {
            return;
        }

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = ipAddress;
        existing.RevokedReason = "LOGOUT";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(
        string? refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var existing = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);

        if (existing is null || existing.RevokedAt.HasValue)
        {
            return;
        }

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = ipAddress;
        existing.RevokedReason = "LOGOUT";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeTokenFamilyAsync(
        Guid familyId,
        string reason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tokens = await _dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = ipAddress;
            token.RevokedReason = reason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private RefreshTokenCreateResult CreateRefreshToken(
        Guid userId,
        Guid familyId,
        string? ipAddress,
        string? userAgent,
        DateTime expiresAt)
    {
        var rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        var now = DateTime.UtcNow;
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = HashRefreshToken(rawToken),
            CreatedAt = now,
            ExpiresAt = expiresAt,
            CreatedByIp = ipAddress,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent
        };

        return new RefreshTokenCreateResult(rawToken, entity);
    }

    private DateTime GetRefreshTokenExpiresAt(DateTime now, bool rememberMe)
    {
        if (rememberMe)
        {
            var days = _options.RefreshTokenDays <= 0 ? 7 : _options.RefreshTokenDays;
            return now.AddDays(days);
        }

        var hours = _options.SessionRefreshTokenHours <= 0 ? 12 : _options.SessionRefreshTokenHours;
        return now.AddHours(hours);
    }

    private MfaChallenge CreateMfaChallenge(
        Guid userId,
        string? ipAddress,
        string? userAgent)
    {
        var now = DateTime.UtcNow;
        var challengeMinutes = _mfaOptions.ChallengeMinutes <= 0 ? 5 : _mfaOptions.ChallengeMinutes;
        return new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Method = TotpMethod,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(challengeMinutes),
            CreatedByIp = ipAddress,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent
        };
    }

    private async Task ConsumeActiveChallengesAsync(
        Guid userId,
        Guid consumedChallengeId,
        DateTime consumedAt,
        CancellationToken cancellationToken)
    {
        var activeChallenges = await _dbContext.MfaChallenges
            .Where(challenge =>
                challenge.UserId == userId
                && challenge.Id != consumedChallengeId
                && challenge.ConsumedAt == null
                && challenge.ExpiresAt > consumedAt)
            .ToListAsync(cancellationToken);

        foreach (var challenge in activeChallenges)
        {
            challenge.ConsumedAt = consumedAt;
        }
    }

    private bool VerifyTotp(string secretKey, string code, DateTime nowUtc)
    {
        var window = _mfaOptions.TotpWindowSteps < 0 ? 1 : _mfaOptions.TotpWindowSteps;
        try
        {
            return _totpAuthenticator.VerifyCode(secretKey, code, nowUtc, window);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ProtectMfaSecret(string secretKey)
    {
        try
        {
            return SensitiveFieldProtector.Protect(secretKey) ?? secretKey;
        }
        catch (InvalidOperationException)
        {
            return secretKey;
        }
    }

    private static string? UnprotectMfaSecret(string? storedSecret)
    {
        if (string.IsNullOrWhiteSpace(storedSecret))
        {
            return null;
        }

        if (!storedSecret.StartsWith("v1:", StringComparison.Ordinal)
            && !storedSecret.StartsWith("v2:", StringComparison.Ordinal))
        {
            return storedSecret;
        }

        try
        {
            return SensitiveFieldProtector.Unprotect(storedSecret);
        }
        catch (InvalidOperationException)
        {
            return storedSecret;
        }
    }

    private static IReadOnlyCollection<string> GetRoleSnapshot(User user)
    {
        return user.UserRoles
            .Select(userRole => userRole.Role?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct()
            .ToList();
    }

    private static string HashRefreshToken(string refreshToken)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }

    private sealed record RefreshTokenCreateResult(string RawToken, RefreshToken Entity);
}
