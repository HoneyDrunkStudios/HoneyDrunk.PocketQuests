using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Administration;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for SystemQuest.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class SystemQuestDataService(AppDbContext context) : BaseDataService<SystemQuestEntity>(context), ISystemQuestDataService
{
    private IQueryable<SystemQuestEntity> SystemQuests => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemQuestEntity>> GetCatalog(CancellationToken cancellationToken = default) =>
        await SystemQuests.ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<SystemQuestEntity?> Editable(string id, CancellationToken token) => SystemQuests.SingleOrDefaultAsync(row => row.Id == id, token);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemQuestStudioRow>> Studio(int limit, CancellationToken token) =>
        await (from head in SystemQuests.AsNoTracking()
               join draft in Context.SystemQuestRevision.AsNoTracking() on head.DraftSystemQuestRevisionId equals draft.Id
               join published in Context.SystemQuestRevision.AsNoTracking() on head.PublishedSystemQuestRevisionId equals published.Id into live
               from published in live.DefaultIfEmpty()
               orderby head.Id
               select new SystemQuestStudioRow(head, draft, published)).Take(limit).ToListAsync(token);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemQuestRevisionEntity>> Published(int limit, CancellationToken token) =>
        await PublishedQuery().OrderBy(row => row.SystemQuestId).Take(limit).ToListAsync(token);

    /// <inheritdoc />
    public Task<SystemQuestRevisionEntity?> ReadPublished(string id, CancellationToken token) =>
        PublishedQuery().SingleOrDefaultAsync(row => row.SystemQuestId == id, token);

    /// <inheritdoc />
    public async Task AddInitialDraft(SystemQuestEntity head, SystemQuestRevisionEntity revision, CancellationToken token)
    {
        if (Context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Initial template creation requires an explicit catalog transaction.");
        await DbSet.AddAsync(head, token);
        await Context.SystemQuestRevision.AddAsync(revision, token);

        // Insert with a null head pointer to break the composite-FK cycle. The
        // pointer and audit are saved by the outer executor, still atomically.
        await Context.SaveChangesAsync(token);
        head.DraftSystemQuestRevisionId = revision.Id;
    }

    private IQueryable<SystemQuestRevisionEntity> PublishedQuery() =>
        from head in SystemQuests.AsNoTracking()
        join revision in Context.SystemQuestRevision.AsNoTracking() on head.PublishedSystemQuestRevisionId equals revision.Id
        select revision;
}
