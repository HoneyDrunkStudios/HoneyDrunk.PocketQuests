using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Models.Quests;
using PocketQuests.Contracts.Requests.Administration;
using PocketQuests.Contracts.Responses.Administration;

namespace PocketQuests.Services.Administration;

/// <summary>Configured-operator system quest authoring and validated previews.</summary>
public interface IAdminSystemQuestService
{
    /// <summary>Reads the bounded template studio.</summary>
    /// <param name="token">Cancellation.</param>
    /// <returns>The authored drafts and starter catalog.</returns>
    Task<SystemQuestStudio> Read(CancellationToken token);

    /// <summary>Validates a preview without saving or publishing anything.</summary>
    /// <param name="request">Proposed system terms.</param>
    /// <returns>The normalized server-calculated preview.</returns>
    Quest Preview(SaveSystemQuestRequest request);

    /// <summary>Appends an immutable draft, preserving any previously published revision.</summary>
    /// <param name="request">Stable identity, proposed terms and expected head version.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The committed draft and current version.</returns>
    Task<AdminSystemQuest> Save(SaveSystemQuestRequest request, CancellationToken token);

    /// <summary>Publishes a reviewed saved draft or withdraws future discovery without changing accepted history.</summary>
    /// <param name="request">Reviewed head and draft identity and desired visibility.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The committed catalog head.</returns>
    Task<AdminSystemQuest> Publish(PublishSystemQuestRequest request, CancellationToken token);
}
