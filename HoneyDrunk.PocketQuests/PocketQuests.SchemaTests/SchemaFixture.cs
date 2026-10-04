using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.Dac;
using PocketQuests.Data.Relational;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PocketQuests.SchemaTests;

/// <summary>Uses only a caller-created, disposable SQL instance and a unique database.</summary>
public sealed partial class SchemaFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim DeploymentGate = new(1);

    private readonly string database = "PocketQuests_SchemaTests_" + Guid.NewGuid().ToString("N");
    private bool deployed;

    /// <summary>Gets safe repeat-publication options.</summary>
    public static DacDeployOptions Options => new()
    {
        BlockOnPossibleDataLoss = true,
        DropObjectsNotInSource = false,
        ScriptDatabaseOptions = false,
        IncludeTransactionalScripts = true,
    };

    /// <summary>Gets the reviewed contract used for source and model comparison.</summary>
    public JsonDocument Contract { get; } = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema-contract.json")));

    /// <summary>Gets the explicitly isolated connection string.</summary>
    public string Connection { get; private set; } = string.Empty;

    /// <summary>Gets the unique database name.</summary>
    public string Database => database;

    /// <summary>Creates the staged mapping context.</summary>
    /// <returns>A no-tracking read context.</returns>
    public RelationalQuestReadContext Context() => new(new DbContextOptionsBuilder<RelationalQuestReadContext>().UseSqlServer(Connection).Options);

    /// <summary>Creates a DacFx connection for deployment and semantic schema comparison.</summary>
    /// <returns>The schema service.</returns>
    public DacServices Services() => new(Connection);

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var instance = Environment.GetEnvironmentVariable("POCKETQUESTS_SCHEMA_TEST_INSTANCE");
        if (instance is null || !InstanceName().IsMatch(instance))
            throw new InvalidOperationException("Run scripts/Test-RelationalSchema.ps1; an isolated PQSchema_<hex> instance is required. Existing product databases are never targets.");
        Connection = new SqlConnectionStringBuilder { DataSource = "(localdb)\\" + instance, InitialCatalog = database, IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = true }.ConnectionString;

        // Concurrent CREATE DATABASE operations compete for LocalDB's model database.
        // Serialize deployment only; account concurrency tests still run real parallel transactions.
        await DeploymentGate.WaitAsync();
        try
        {
            using var package = DacPackage.Load(Path.Combine(AppContext.BaseDirectory, "PocketQuests.Database.dacpac"));
            Services().Deploy(package, database, upgradeExisting: false, options: Options);
            deployed = true;
        }
        finally
        {
            DeploymentGate.Release();
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Test-only SQL from checked-in fixtures; no request data. Database name is a locally generated GUID on an explicitly isolated instance.")]
    public async Task DisposeAsync()
    {
        Contract.Dispose();
        if (!deployed)
            return;
        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(Connection) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();

        // This identifier is generated above, never accepted from the environment or a request.
        await using var command = new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];", connection);
        await command.ExecuteNonQueryAsync();
        foreach (var backup in backups)
            File.Delete(backup);
    }

    /// <summary>Executes a synthetic SQL batch in the fixture database.</summary>
    /// <param name="sql">Reviewed test SQL.</param>
    /// <returns>Completion.</returns>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Test-only SQL from checked-in fixtures; no request data. Database name is a locally generated GUID on an explicitly isolated instance.")]
    public async Task Execute(string sql)
    {
        await using var connection = new SqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Executes GO-separated probes on one connection so temporary procedures and rollback survive.</summary>
    /// <param name="name">The checked-in SQL filename.</param>
    /// <returns>Completion after every batch succeeds.</returns>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Test-only SQL from checked-in fixtures; no request data. Database name is a locally generated GUID on an explicitly isolated instance.")]
    public async Task Script(string name)
    {
        await using var connection = new SqlConnection(Connection);
        await connection.OpenAsync();
        var text = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", name));
        foreach (var batch in BatchSeparator().Split(text).Where(batch => !string.IsNullOrWhiteSpace(batch)))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 60 };
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Queries catalog evidence with the current disposable connection.</summary>
    /// <param name="sql">A read-only query.</param>
    /// <returns>All result sets in order.</returns>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Test-only SQL from checked-in fixtures; no request data. Database name is a locally generated GUID on an explicitly isolated instance.")]
    public async Task<List<DataTable>> Query(string sql)
    {
        await using var connection = new SqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<DataTable>();
        do
        {
            var table = new DataTable();
            for (var i = 0; i < reader.FieldCount; i++)
                table.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
            while (await reader.ReadAsync())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                table.Rows.Add(values);
            }

            results.Add(table);
        }
        while (await reader.NextResultAsync());
        return results;
    }

    [GeneratedRegex("^PQSchema_[0-9a-f]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex InstanceName();

    [GeneratedRegex("^\\s*GO\\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BatchSeparator();
}
