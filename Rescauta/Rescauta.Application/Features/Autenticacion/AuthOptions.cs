namespace Rescauta.Application.Features.Autenticacion;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int PasswordMinLength { get; set; } = 8;

    public int MaxFailedAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    public int SessionHours { get; set; } = 12;

    public int RememberMeDays { get; set; } = 30;

    public int ResetTokenMinutes { get; set; } = 30;

    public bool ExposeResetLinkInDevelopment { get; set; }

    public string? SeedAdminEmail { get; set; }

    public string? SeedAdminPassword { get; set; }
}
