namespace NVGInventory.Domain.Constants;

public static class AuthFailureReasons
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string UserInactive = "USER_INACTIVE";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string MfaRequired = "MFA_REQUIRED";
    public const string MfaNotEnabled = "MFA_NOT_ENABLED";
    public const string MfaChallengeExpired = "MFA_CHALLENGE_EXPIRED";
    public const string InvalidMfaCode = "INVALID_MFA_CODE";
}
