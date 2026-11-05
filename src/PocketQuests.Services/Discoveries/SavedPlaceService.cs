using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using PocketQuests.Contracts.Models.Discoveries;
using PocketQuests.Contracts.Requests.Discoveries;
using PocketQuests.Contracts.Responses.Discoveries;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Discoveries;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Discoveries;
using PocketQuests.Domain.Errors;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Discoveries.Mapping;
using PocketQuests.Services.Discoveries.Validators;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Quests.Mapping;
using System.Collections.Immutable;

namespace PocketQuests.Services.Discoveries;

/// <summary>Persists private discoveries under the existing account lock, lifecycle policy and transaction.</summary>
public sealed class SavedPlaceService(IAccountDataService accounts, ISavedPlaceDataService places, IAccountAuditRecordDataService audits, QuestService quests, ICurrentAccount currentAccount, TimeProvider clock) : ISavedPlaceService
{
    /// <inheritdoc />
    public Task<ImmutableArray<SavedPlace>> Read(CancellationToken token = default) => accounts.ExecuteInTransaction(
            async ct =>
    {
        var identity = currentAccount.Identity.ToModel();
        await quests.RequireAccess(identity, true, ct);
        var account = await quests.Account(identity, ct);
        return (await places.GetByAccountId(account.Id, ct)).Select(row => row.ToContract()).ToImmutableArray();
    },
            token);

    /// <inheritdoc />
    public async Task<SavedPlace> Save(SavePlaceRequest request, CancellationToken token = default)
    {
        SavedPlaceValidator.Validate(request);
        var now = clock.GetUtcNow().ToUniversalTime();
        var row = await accounts.ExecuteInTransaction(
            async ct =>
        {
            var identity = currentAccount.Identity.ToModel();
            await quests.RequireAccess(identity, true, ct);
            var account = await quests.Account(identity, ct);
            var place = await FindOrCreate(account.Id, request, now, ct);

            if (request.QuestOccurrenceId is { } occurrence && !await places.CanLink(account.Id, occurrence, ct))
                throw new QuestValidationException("Choose one of your accepted quests.");
            SavedPlaceMapping.Apply(request, place);
            place.ModifiedAt = now > place.ModifiedAt ? now : place.ModifiedAt;
            place.VisitedAt = request.Visited ? place.VisitedAt ?? now : null;
            place.Revision++;
            await Audit(account.Id, identity.Subject, now, "save", ct);
            return place;
        },
            token);
        return row.ToContract();
    }

    /// <inheritdoc />
    public async Task<RemovalResult> Remove(RemovePlaceRequest request, CancellationToken token = default)
    {
        if (request.Id == Guid.Empty || request.ExpectedRevision < 1)
            throw new QuestValidationException("Choose a saved place to remove.");
        return await accounts.ExecuteInTransaction(
            async ct =>
        {
            var identity = currentAccount.Identity.ToModel();
            await quests.RequireAccess(identity, true, ct);
            var account = await quests.Account(identity, ct);
            var row = (await places.GetByAccountId(account.Id, ct)).SingleOrDefault(item => item.Id == request.Id);
            if (row is null)
                return new RemovalResult(true);
            if (row.Revision != request.ExpectedRevision)
                throw new QuestConflictException("This place changed. Reload your journal before removing it.");
            places.Remove(row);
            await Audit(account.Id, identity.Subject, clock.GetUtcNow().ToUniversalTime(), "remove", ct);
            return new RemovalResult(true);
        },
            token);
    }

    private async Task<SavedPlaceEntity> FindOrCreate(Guid accountId, SavePlaceRequest request, DateTimeOffset now, CancellationToken token)
    {
        var existing = await places.GetByAccountId(accountId, token);
        var place = existing.SingleOrDefault(item => item.Id == request.Id);
        if (place is not null)
        {
            if (place.Revision != request.ExpectedRevision || place.PlaceKey != request.PlaceKey)
                throw new QuestConflictException("This place changed. Reload your journal before editing.");
            return place;
        }

        if (request.ExpectedRevision != 0 || existing.Any(item => item.PlaceKey == request.PlaceKey))
            throw new QuestConflictException("This place changed. Reload your journal before editing.");
        if (existing.Count >= SavedPlaceValidator.MaximumPlaces)
            throw new QuestValidationException("Your journal holds 100 places. Remove a place before saving another.");
        if (await places.FindByIdAsync(request.Id, token) is not null)
            throw new QuestConflictException("Choose a new saved-place identifier.");
        place = new SavedPlaceEntity { Id = request.Id, AccountId = accountId, PlaceKey = request.PlaceKey, CreatedAt = now };
        await places.AddAsync(place, token);
        return place;
    }

    private async Task Audit(Guid accountId, string subject, DateTimeOffset at, string action, CancellationToken token)
    {
        var audit = AuditRecord.FromEntry(new AuditEntry(AuditEntryId.New(), at, subject, "pocketquests.discovery." + action, AuditCategory.UserActivity, AuditOutcome.Succeeded, new AuditTarget("quest.account", accountId.ToString("N")), TenantId.Internal, null, AuditOperation.Update));
        await audits.AddWithAudit(new AccountAuditRecordEntity { AccountId = accountId, AuditRecordId = audit.Id, CreatedAt = at }, audit, token);
    }
}
