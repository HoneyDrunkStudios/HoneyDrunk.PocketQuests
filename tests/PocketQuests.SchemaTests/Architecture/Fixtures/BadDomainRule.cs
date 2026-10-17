namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal static class BadDomainRule
{
    internal static bool Evaluate(string path) => File.Exists(path);
}
