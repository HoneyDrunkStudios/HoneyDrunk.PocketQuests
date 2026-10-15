namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal abstract class ConcreteRepositoryService(PocketQuests.Data.DataServices.BaseDataService<PocketQuests.Data.Entities.Accounts.AccountEntity> data)
{
    internal object Data => data;
}
