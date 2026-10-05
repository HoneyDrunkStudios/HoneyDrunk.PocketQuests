using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Lifecycle;
using PocketQuests.Tests.Fixtures;
using System.Security.Cryptography;
using System.Text;

namespace PocketQuests.Tests.Integration;

/// <summary>Canonical persistence helpers shared by both real-SQL regression hosts.</summary>
public sealed partial class SqlApiTests
{
    private PersistenceServices? persistence;

    private static AccountIdentity TestIdentity(string subject) => new("honeydrunk-identity", "usr_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject)))[..26]);

    private ProductDatabase Context() => new(Connection);

    private TestQuestWorkflow Store() => Services().Resolve<TestQuestWorkflow>();

    private TestQuestWorkflow Commands() => Services().Resolve<TestQuestWorkflow>();

    private LifecycleService Lifecycle() => Services().Resolve<LifecycleService>();

    private PersistenceServices Services() => persistence ??= new(Connection);

    private ValueTask DisposePersistence() => persistence?.DisposeAsync() ?? ValueTask.CompletedTask;
}
