using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

namespace PocketQuests.DevelopmentHost;

internal static class DevelopmentDatabase
{
    internal static string Connection(string database, string instance) => new SqlConnectionStringBuilder
    {
        DataSource = "(localdb)\\" + instance,
        InitialCatalog = database,
        IntegratedSecurity = true,
        Encrypt = true,
        TrustServerCertificate = true,
    }.ConnectionString;

    internal static async Task EnsureCreated(string database, string dacpac, string instance)
    {
        // These two databases belong exclusively to this tool. Never reset,
        // migrate or delete an existing database when starting/stopping it.
        if (database is not ("PocketQuests_ExpoGo" or "PocketQuests_ExpoGo_Identity"))
            throw new InvalidOperationException("Only the dedicated Expo Go databases are allowed.");
        var masterConnection = new SqlConnectionStringBuilder(Connection(database, instance)) { InitialCatalog = "master" };
        await using var master = new SqlConnection(masterConnection.ConnectionString);
        await master.OpenAsync();
        await using var check = new SqlCommand("SELECT DB_ID(@database)", master);
        check.Parameters.AddWithValue("@database", database);
        if (await check.ExecuteScalarAsync() is not DBNull)
            return;
        using var package = DacPackage.Load(Path.Combine(AppContext.BaseDirectory, dacpac + ".dacpac"));
        var service = new DacServices(masterConnection.ConnectionString);
        service.Deploy(package, database, upgradeExisting: false, options: new DacDeployOptions
        {
            BlockOnPossibleDataLoss = true,
            DropObjectsNotInSource = false,
            ScriptDatabaseOptions = false,
            IncludeTransactionalScripts = true,
        });
    }
}
