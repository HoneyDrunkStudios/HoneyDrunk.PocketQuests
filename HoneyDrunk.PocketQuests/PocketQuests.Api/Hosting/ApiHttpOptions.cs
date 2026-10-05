namespace PocketQuests.Api.Hosting;

/// <summary>Local process HTTP limits and explicitly trusted browser/proxy configuration.</summary>
public sealed class ApiHttpOptions
{
    /// <summary>Gets the browser origins allowed to read API responses.</summary>
    public List<string> AllowedOrigins { get; } = [];

    /// <summary>Gets the immediate reverse proxy IP addresses allowed to supply forwarded headers.</summary>
    public List<string> KnownProxies { get; } = [];

    /// <summary>Gets or sets the external TLS port used by HTTPS redirection.</summary>
    public int HttpsPort { get; set; } = 443;

    /// <summary>Gets or sets the number of commands allowed per authenticated account per window.</summary>
    public int CommandPermitLimit { get; set; } = 120;

    /// <summary>Gets or sets the number of exports allowed per authenticated account per window.</summary>
    public int ExportPermitLimit { get; set; } = 5;

    /// <summary>Gets or sets the fixed rate window in seconds.</summary>
    public int RateWindowSeconds { get; set; } = 60;
}
