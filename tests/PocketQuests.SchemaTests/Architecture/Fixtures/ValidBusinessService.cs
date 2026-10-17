namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal abstract class ValidBusinessService(PocketQuests.Data.DataServices.IBaseDataService<PocketQuests.Data.Entities.Accounts.AccountEntity> data)
{
    internal object Data => data;
}
