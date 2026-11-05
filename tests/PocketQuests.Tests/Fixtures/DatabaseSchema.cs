using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.SqlServer.Dac;

namespace PocketQuests.Tests.Fixtures;

/// <summary>Deploys the production SQL project into isolated integration-test databases.</summary>
internal static class DatabaseSchema
{
    private const int SchemaCommandTimeoutSeconds = 300;

    internal static Task DeployAsync(DatabaseFacade database, string project = "PocketQuests.Database") => Task.Run(() =>
    {
        // A pooled DacFx model connection can retain a shared database lock and block CREATE DATABASE.
        var connection = new SqlConnectionStringBuilder(database.GetConnectionString()) { Pooling = false };
        if (!connection.InitialCatalog.StartsWith("PocketQuests_Tests_", StringComparison.Ordinal))
            throw new InvalidOperationException("DACPAC test deployment requires an isolated test database.");

        // Release the model connection before DacFx inspects the empty target,
        // matching the isolated package-mode fixture's creation sequence.
        var master = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" };
        using (var sql = new SqlConnection(master.ConnectionString))
        {
            sql.Open();
            using var exists = new SqlCommand("SELECT DB_ID(@database)", sql);
            exists.Parameters.AddWithValue("@database", connection.InitialCatalog);
            if (exists.ExecuteScalar() is DBNull)
            {
                using var create = new SqlCommand("DECLARE @statement nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@database); EXEC sys.sp_executesql @statement;", sql);

                // Hosted cold-start database creation needs the same bounded budget as DACPAC deployment.
                create.CommandTimeout = SchemaCommandTimeoutSeconds;
                create.Parameters.AddWithValue("@database", connection.InitialCatalog);
                create.ExecuteNonQuery();
            }
        }

        using var package = DacPackage.Load(Path.Combine(AppContext.BaseDirectory, project + ".dacpac"));
        var service = new DacServices(connection.ConnectionString);
        service.Deploy(package, connection.InitialCatalog, upgradeExisting: true, options: new DacDeployOptions
        {
            CommandTimeout = SchemaCommandTimeoutSeconds,
            BlockOnPossibleDataLoss = true,
            DropObjectsNotInSource = false,
            ScriptDatabaseOptions = false,
            IncludeTransactionalScripts = true,
        });
    });
}
