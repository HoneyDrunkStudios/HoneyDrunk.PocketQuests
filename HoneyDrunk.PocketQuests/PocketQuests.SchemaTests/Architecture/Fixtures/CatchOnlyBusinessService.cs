namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal static class CatchOnlyBusinessService
{
    internal static bool Attempt(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return false;
        }
    }
}
