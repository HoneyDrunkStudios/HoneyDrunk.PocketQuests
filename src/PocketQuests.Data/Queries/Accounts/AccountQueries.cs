using Microsoft.EntityFrameworkCore;

namespace PocketQuests.Data.Queries.Accounts;

internal static class AccountQueries
{
    internal static async Task AcquireLock(AppDbContext context, string identityUserId, CancellationToken token)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("An explicit account transaction is required.");
        var resource = "pocketquests:" + identityUserId;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
            IF @result<0 THROW 51102, 'Account lock unavailable.', 1;
            """,
            token);
    }
}
