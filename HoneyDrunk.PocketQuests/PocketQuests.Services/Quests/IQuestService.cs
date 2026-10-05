using PocketQuests.Contracts.Requests.Commands;
using PocketQuests.Contracts.Responses.Projections;

namespace PocketQuests.Services.Quests;

/// <summary>Public quest operations over validated commands and durable account history.</summary>
public interface IQuestService
{
    /// <summary>Executes an authenticated command or returns its original receipt response.</summary>
    /// <param name="request">Existing public command contract.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The authoritative command response.</returns>
    Task<QuestState> Execute(QuestCommand request, CancellationToken token = default);

    /// <summary>Projects current owned sources without writing account rows or reward projections.</summary>
    /// <param name="token">Cancellation.</param>
    /// <returns>The authenticated account state.</returns>
    Task<QuestState> Read(CancellationToken token = default);
}
