using PocketQuests.Contracts.Requests.Profiles;
using PocketQuests.Contracts.Responses.Projections;

namespace PocketQuests.Services.Profiles;

/// <summary>Explicit, idempotent profile creation for the authenticated account.</summary>
public interface IProfileService
{
    /// <summary>Initializes the account and returns its current source-derived state.</summary>
    /// <param name="request">Initial time zone.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The existing account or newly initialized profile.</returns>
    Task<QuestState> Initialize(InitializeProfile request, CancellationToken token = default);
}
