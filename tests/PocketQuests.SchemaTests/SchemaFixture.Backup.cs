using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace PocketQuests.SchemaTests;

/// <summary>Actual backup/restore is restricted to this fixture's generated database and generated temporary backup files.</summary>
public sealed partial class SchemaFixture
{
    private readonly HashSet<string> backups = [];

    /// <summary>Creates an actual SQL backup of only this fixture's disposable database.</summary>
    /// <returns>The fixture-owned temporary backup path.</returns>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Database identifier is the private fixture-generated GUID; backup path is parameterized and generated under the current temporary directory.")]
    public async Task<string> Backup()
    {
        if (!deployed)
            throw new InvalidOperationException("Only a deployed isolated fixture may be backed up.");
        var path = Path.Combine(Path.GetTempPath(), "pq-schema-backup-" + Guid.NewGuid().ToString("N") + ".bak");
        if (File.Exists(path))
            throw new InvalidOperationException("Refusing to reuse a backup file.");
        backups.Add(path);
        await using var connection = new SqlConnection(new SqlConnectionStringBuilder(Connection) { InitialCatalog = "master" }.ConnectionString);
        await connection.OpenAsync();
        await using var sql = new SqlCommand($"BACKUP DATABASE [{database}] TO DISK=@path WITH COPY_ONLY, CHECKSUM;", connection) { CommandTimeout = 120 };
        sql.Parameters.Add("@path", SqlDbType.NVarChar, 4000).Value = path;
        await sql.ExecuteNonQueryAsync();
        return path;
    }

    /// <summary>Restores only an actual backup created by this fixture over the same isolated disposable database.</summary>
    /// <param name="path">Path returned by this fixture's Backup method.</param>
    /// <returns>Completion while the restored database is still withheld from product traffic by the test.</returns>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Only the private GUID-named fixture database on PQSchema_<hex> is overwritten; backup path must belong to this fixture's generated allowlist.")]
    public async Task Restore(string path)
    {
        if (!deployed || !backups.Contains(path))
            throw new InvalidOperationException("Only this fixture's own generated backup may be restored.");
        await using var pooled = new SqlConnection(Connection);
        SqlConnection.ClearPool(pooled);
        await using var connection = new SqlConnection(new SqlConnectionStringBuilder(Connection) { InitialCatalog = "master" }.ConnectionString);
        await connection.OpenAsync();
        await using var sql = new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [{database}] FROM DISK=@path WITH REPLACE,CHECKSUM; ALTER DATABASE [{database}] SET MULTI_USER;", connection) { CommandTimeout = 120 };
        sql.Parameters.Add("@path", SqlDbType.NVarChar, 4000).Value = path;
        await sql.ExecuteNonQueryAsync();
    }
}
