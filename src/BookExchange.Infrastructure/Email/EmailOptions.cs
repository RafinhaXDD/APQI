namespace BookExchange.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "no-reply@aqpi.local";

    public string FromName { get; set; } = "AQPI";

    /// <summary>Public URL of the web app; links in emails point here.</summary>
    public Uri AppBaseUrl { get; set; } = new("http://localhost:5173");

    public SmtpSettings Smtp { get; set; } = new();
}

public sealed class SmtpSettings
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public bool UseStartTls { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}
