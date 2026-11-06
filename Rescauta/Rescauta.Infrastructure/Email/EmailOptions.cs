namespace Rescauta.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? User { get; set; }

    public string? Password { get; set; }

    public string From { get; set; } = "no-reply@rescauta.local";

    public string FromName { get; set; } = "Rescauta";
}
