namespace NVGInventory.Security;

public sealed class MfaOptions
{
    public const string SectionName = "Mfa";

    public string Issuer { get; set; } = "NVG Dispatch";
    public int ChallengeMinutes { get; set; } = 5;
    public int StepUpMinutes { get; set; } = 15;
    public int TotpWindowSteps { get; set; } = 1;
}
