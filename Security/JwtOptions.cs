namespace NVGInventory.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
    public int SessionRefreshTokenHours { get; set; } = 12;
    public int ClockSkewMinutes { get; set; } = 1;
}
