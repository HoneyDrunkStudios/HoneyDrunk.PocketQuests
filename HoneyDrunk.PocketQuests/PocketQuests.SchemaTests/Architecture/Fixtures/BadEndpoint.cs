namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal static class BadEndpoint
{
    internal static object Read(PocketQuests.Data.DataServices.IBaseDataService<PocketQuests.Data.Entities.Accounts.AccountEntity> data) => data;
}
