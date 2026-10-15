namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal abstract class ValidContract
{
    internal Task<IReadOnlyList<Guid[]>>? AccountIds { get; init; }
}
