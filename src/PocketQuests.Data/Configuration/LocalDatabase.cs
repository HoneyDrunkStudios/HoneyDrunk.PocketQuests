namespace PocketQuests.Data.Configuration;

/// <summary>Local development connection defaults used only by migration tooling.</summary>
public static class LocalDatabase
{
    /// <summary>The dedicated local SQL Server database; production connections must be configured separately.</summary>
    public const string ConnectionString = "Server=(localdb)\\PocketQuests;Database=PocketQuests;Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
}
