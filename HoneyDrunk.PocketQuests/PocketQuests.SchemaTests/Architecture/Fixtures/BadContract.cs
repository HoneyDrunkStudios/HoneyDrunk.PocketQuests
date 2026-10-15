namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal abstract class BadContract
{
    internal Task<IReadOnlyList<PocketQuests.Data.Entities.Accounts.AccountEntity[]>>? Accounts { get; init; }
}
