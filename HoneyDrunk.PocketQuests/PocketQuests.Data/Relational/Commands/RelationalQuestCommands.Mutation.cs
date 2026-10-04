using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using PocketQuests.Application.Identity;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Schedules;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Maps all existing command lanes to typed current rows, immutable terms and compact replay history.</summary>
public sealed partial class RelationalQuestCommands
{
    private static async Task CommitMutation(Session session, AccountIdentity identity, AccountEntity account, QuestCommand command, byte[] digest, QuestAggregate aggregate, QuestState state, DateTimeOffset recordedAt, DateTimeOffset reconciledAt, DateTimeOffset projectionAt, DateTimeOffset now, CancellationToken token, int reconciliationLimit = int.MaxValue, int actionReconciliationLimit = int.MaxValue, bool internalTransition = false, bool hasPending = false)
    {
        var batch = await MutationBindings.Read(session.Context, account, token);
        var writer = new MutationWriter(batch, account, command, aggregate, state, recordedAt, reconciledAt, projectionAt, now, await ReadTerms(session, account.Id, token), reconciliationLimit, actionReconciliationLimit, internalTransition, hasPending);
        writer.Prepare();
        await using var sql = session.Procedure("pocketquests.CommitAccountMutation");
        Add(sql, "@IdentityUserId", SqlDbType.VarChar, identity.Subject, 30);
        Add(sql, "@ExpectedVersion", SqlDbType.BigInt, account.MutationVersion);
        Add(sql, "@IsInternal", SqlDbType.Bit, internalTransition);
        Add(sql, "@OperationId", SqlDbType.UniqueIdentifier, command.OperationId);
        Add(sql, "@Action", SqlDbType.VarChar, command.Action, 40);
        Add(sql, "@Digest", SqlDbType.Binary, digest, 32);

        // V2 derives original feedback from typed history. Even the largest legal custom allocation
        // leaves this receipt constant in size instead of copying names/unlocks or account state.
        Add(sql, "@Outcome", SqlDbType.NVarChar, JsonSerializer.Serialize(new CompactOutcome(projectionAt, null)), -1);
        Add(sql, "@RecordedAt", SqlDbType.DateTimeOffset, recordedAt);
        Add(sql, "@ProjectionAt", SqlDbType.DateTimeOffset, projectionAt);
        Add(sql, "@Now", SqlDbType.DateTimeOffset, now);
        Add(sql, "@AnchorId", SqlDbType.UniqueIdentifier, command.RecordedTime?.AnchorId);
        Add(sql, "@BootId", SqlDbType.UniqueIdentifier, command.RecordedTime?.BootId);
        Add(sql, "@Ordinal", SqlDbType.BigInt, command.RecordedTime?.Ordinal);
        Add(sql, "@Elapsed", SqlDbType.Float, command.RecordedTime?.ElapsedMilliseconds);
        MutationBindings.BindAll(sql, batch);
        var audit = AuditRecord.FromEntry(new AuditEntry(
            AuditEntryId.New(),
            now,
            identity.Subject,
            "pocketquests.quest." + command.Action,
            AuditCategory.UserActivity,
            AuditOutcome.Succeeded,
            new AuditTarget("quest.account", account.Id.ToString("N")),
            TenantId.Internal,
            Activity.Current?.TraceId.ToString(),
            AuditOperation.Update,
            Metadata: new Dictionary<string, string>
            {
                ["operationId"] = command.OperationId.ToString("N"),
                ["rulesetVersion"] = "1.0",
            }));
        Add(sql, "@AuditId", SqlDbType.VarChar, audit.Id, 32);
        Add(sql, "@AuditCategory", SqlDbType.Int, (int)audit.Category);
        Add(sql, "@AuditOutcome", SqlDbType.Int, (int)audit.Outcome);
        Add(sql, "@AuditOperation", SqlDbType.Int, (int)audit.Operation);
        Add(sql, "@AuditTenant", SqlDbType.VarChar, audit.TenantId, 100);
        Add(sql, "@AuditCorrelation", SqlDbType.NVarChar, audit.CorrelationId, -1);
        Add(sql, "@AuditChanges", SqlDbType.NVarChar, audit.ChangesJson, -1);
        Add(sql, "@AuditMetadata", SqlDbType.NVarChar, audit.MetadataJson, -1);
        await sql.ExecuteNonQueryAsync(token);
    }

    private sealed class MutationWriter(MutationBatch batch, AccountEntity account, QuestCommand command, QuestAggregate aggregate, QuestState state, DateTimeOffset recordedAt, DateTimeOffset reconciledAt, DateTimeOffset projectionAt, DateTimeOffset now, Dictionary<Guid, Quest> terms, int reconciliationLimit, int actionReconciliationLimit, bool internalTransition, bool hasPending)
    {
        private readonly Dictionary<Guid, Guid> _occurrenceRevisions = [];

        private long Version => account.MutationVersion + 1;

        private Guid? ReceiptId => internalTransition ? null : command.OperationId;

        internal void Prepare()
        {
            Profile();
            foreach (var definition in aggregate.Definitions)
            {
                _ = Terms(definition.Quest, definition.Revision);
                var row = MutationBatch.Copy(batch.All<QuestDefinitionEntity>().Single(r => r.Id == Guid.Parse(definition.Quest.Id)));
                row.Revision = definition.Revision;
                row.CreationOrdinal = aggregate.Definitions.IndexOf(definition) + 1;
                row.ArchivedAt = definition.Archived ? row.ArchivedAt ?? recordedAt : null;
                row.ModifiedAt = now;
                batch.Put(row);
            }

            Series();
            Occurrences();
            Completions();
            Projections();
            History();
        }

        private static DateOnly? Date(string? value) => value is null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static TimeOnly? Time(string? value) => value is null ? null : TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);

        private static (string? system, Guid? custom) Skill(string id) => Guid.TryParseExact(id, "D", out var custom) ? (null, custom) : (id, null);

        private Guid Terms(Quest quest, int? revision = null)
        {
            var definitionId = quest.IsCustom ? Guid.Parse(quest.Id) : Derived(account.Id, "definition/" + quest.Id);
            var identity = batch.All<QuestDefinitionEntity>().SingleOrDefault(d => d.Id == definitionId);
            if (identity is null)
            {
                identity = new()
                {
                    Id = definitionId,
                    AccountId = account.Id,
                    SystemQuestId = quest.IsCustom ? null : quest.Id,
                    ClientKey = quest.IsCustom ? quest.Id : null,
                    Revision = revision ?? 1,
                    CreatedAt = now,
                    ModifiedAt = now
                };
                batch.Put(identity);
            }

            var serialized = JsonSerializer.Serialize(quest);
            var candidates = batch.All<QuestDefinitionRevisionEntity>().Where(r => r.QuestDefinitionId == definitionId && (revision is null || r.Revision == revision))
                .OrderByDescending(r => r.Revision);
            foreach (var candidate in candidates)
            {
                if (terms.TryGetValue(candidate.Id, out var existing) && JsonSerializer.Serialize(existing) == serialized)
                    return candidate.Id;
            }

            var number = revision ?? batch.All<QuestDefinitionRevisionEntity>().Where(r => r.QuestDefinitionId == definitionId).Select(r => r.Revision).DefaultIfEmpty(0).Max() + 1;
            var id = Derived(account.Id, $"definition/{definitionId:D}/revision/{number}");
            batch.Put(new QuestDefinitionRevisionEntity
            {
                Id = id,
                AccountId = account.Id,
                QuestDefinitionId = definitionId,
                Revision = number,
                Title = quest.Title,
                Criterion = quest.Criterion,
                Description = quest.Description,
                CategoryId = quest.CategoryId,
                RankCode = quest.Rank.ToString(),
                EffortCode = quest.Effort.ToString(),
                BaseXp = quest.BaseXp,
                PenaltyPercent = checked((byte)quest.PenaltyPercent),
                RulesetVersion = "1.0",
                DisplaySnapshotVersion = 2,
                DisplaySnapshotJson = JsonSerializer.Serialize(new { CategoryName = Catalog.Categories.Single(c => c.Id == quest.CategoryId).Name }),
                EffectiveAt = recordedAt,
                CommandReceiptId = ReceiptId,
                CreatedAt = now,
            });
            for (var index = 0; index < quest.Attributes.Length; index++)
            {
                var allocation = quest.Attributes[index];
                batch.Put(new QuestDefinitionAttributeAllocationEntity
                {
                    AccountId = account.Id,
                    QuestDefinitionRevisionId = id,
                    AttributeId = allocation.Id,
                    BasisPoints = allocation.BasisPoints,
                    Position = index,
                    CreatedAt = now
                });
            }

            for (var index = 0; index < quest.Skills.Length; index++)
            {
                var allocation = quest.Skills[index];
                var (system, custom) = Skill(allocation.Id);
                batch.Put(new QuestDefinitionSkillAllocationEntity
                {
                    Id = Derived(account.Id, $"allocation/{id:D}/{allocation.Id}"),
                    AccountId = account.Id,
                    QuestDefinitionRevisionId = id,
                    SystemSkillId = system,
                    CustomSkillId = custom,
                    BasisPoints = allocation.BasisPoints,
                    Position = index,
                    CreatedAt = now
                });
            }

            terms[id] = quest;
            return id;
        }

        private void Profile()
        {
            var profile = aggregate.Profile;
            var row = MutationBatch.Copy(account);
            row.TimeZoneId = aggregate.Zone;
            row.IsOnboardingComplete = profile.OnboardingComplete;
            row.HasExpiryWarnings = profile.ExpiryWarnings;
            row.IsAccountPaused = aggregate.Schedule.AccountPaused;
            row.SelectedBadgeId = state.Profile.BadgeId;
            row.SelectedFrameId = state.Profile.FrameId;
            row.MutationVersion = Version;
            row.ProjectionVersion = Version;
            row.ProjectionAsOfAt = hasPending ? (account.ProjectionAsOfAt < recordedAt ? account.ProjectionAsOfAt : recordedAt) : projectionAt;
            row.HasPendingReconciliation = hasPending;
            row.LastRecordedAt = projectionAt;
            row.ModifiedAt = now;
            batch.Put(row);
            foreach (var interest in profile.Interests)
                batch.Put(new AccountInterestEntity { AccountId = account.Id, CategoryId = interest, Position = profile.Interests.IndexOf(interest), CreatedAt = now });
            foreach (var skill in profile.CustomSkills ?? [])
            {
                var id = Guid.Parse(skill.Id);
                var prior = batch.Original<CustomSkillEntity>().SingleOrDefault(s => s.Id == id);
                batch.Put(new CustomSkillEntity
                {
                    Id = id,
                    AccountId = account.Id,
                    Name = skill.Name,
                    ClientKey = skill.Id,
                    CreationOrdinal = profile.CustomSkills!.IndexOf(skill) + 1,
                    NormalizedName = skill.Name.ToUpperInvariant(),
                    NameNormalizationVersion = 1,
                    Revision = skill.Revision,
                    ArchivedAt = skill.Archived ? prior?.ArchivedAt ?? recordedAt : null,
                    CreatedAt = now,
                    ModifiedAt = now
                });
            }

            if (command.Action == QuestActions.AssessSkill)
            {
                var (system, custom) = Skill(command.SkillId!);
                batch.Put(new SkillAssessmentEntity
                {
                    Id = command.OperationId,
                    AccountId = account.Id,
                    SystemSkillId = system,
                    CustomSkillId = custom,
                    ExperienceCode = command.Experience!.Value.ToString(),
                    SeedXp = Progression.Seed(command.Experience.Value),
                    RulesetVersion = "1.0",
                    EffectiveAt = recordedAt,
                    CommandReceiptId = command.OperationId,
                    CreatedAt = now
                });
            }

            if (command.Action == QuestActions.Zone && aggregate.Zone != account.TimeZoneId)
            {
                batch.Put(new TimeZoneChangeEntity
                {
                    Id = command.OperationId,
                    AccountId = account.Id,
                    FromTimeZoneId = account.TimeZoneId,
                    ToTimeZoneId = aggregate.Zone,
                    EffectiveAt = recordedAt,
                    CommandReceiptId = command.OperationId,
                    CreatedAt = now
                });
            }

            for (var index = 0; index < aggregate.Schedule.Pauses.Length; index++)
            {
                var pause = aggregate.Schedule.Pauses[index];
                var id = Derived(account.Id, $"pause/{index}/{pause.CategoryId}/{pause.StartedAt:O}");
                var prior = batch.Original<AccountPauseEntity>().SingleOrDefault(p => p.Id == id);
                batch.Put(new AccountPauseEntity
                {
                    Id = id,
                    AccountId = account.Id,
                    ScopeCode = "Category",
                    CreationOrdinal = index + 1,
                    CategoryId = pause.CategoryId,
                    StartedAt = pause.StartedAt,
                    EndedAt = pause.EndedAt,
                    CommandReceiptId = prior is null ? ReceiptId : prior.CommandReceiptId,
                    CreatedAt = now,
                    ModifiedAt = now
                });
            }
        }

        private void Series()
        {
            foreach (var series in aggregate.Schedule.Series)
            {
                var termId = Terms(series.Quest);
                var prior = batch.Original<QuestSeriesEntity>().SingleOrDefault(s => s.Id == series.Id);
                var oldRevision = prior is null ? null : batch.Original<QuestSeriesRevisionEntity>().Single(r => r.QuestSeriesId == series.Id && r.Revision == prior.Revision);
                var revision = new QuestSeriesRevisionEntity
                {
                    Id = oldRevision?.Id ?? Guid.NewGuid(),
                    AccountId = account.Id,
                    QuestSeriesId = series.Id,
                    Revision = prior?.Revision ?? 1,
                    QuestDefinitionRevisionId = termId,
                    CadenceCode = series.Cadence.ToString(),
                    Interval = series.Interval,
                    AnchorOn = Date(series.Anchor)!.Value,
                    PlannedTime = Time(series.PlannedTime),
                    HasAutoAcceptPenalty = series.AutoAcceptPenalty,
                    EffectiveAt = series.EffectiveAt ?? recordedAt,
                    ScheduleVersion = series.Version,
                    CommandReceiptId = internalTransition ? oldRevision?.CommandReceiptId ?? throw new InvalidOperationException("An internal transition cannot introduce new series configuration.") : command.OperationId,
                    CreatedAt = now
                };
                if (oldRevision is null || !MutationBatch.Same(oldRevision, revision, "CommandReceiptId"))
                {
                    revision.Revision = (prior?.Revision ?? 0) + 1;
                    revision.Id = Derived(account.Id, $"series/{series.Id:D}/revision/{revision.Revision}");
                    batch.Put(revision);
                }
                else
                {
                    revision = oldRevision;
                }

                DateOnly? next = null;
                if (!series.Stopped)
                {
                    try
                    {
                        var date = Scheduling.Recurrence(Scheduling.ParseDate(series.Anchor), series.Cadence, series.Interval, series.NextSequence, series.PauseDays);
                        if (date.Year < 9999)
                            next = Date(Scheduling.DateText(date));
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // End-of-calendar recurrence has no further delivery date.
                    }
                }

                batch.Put(new QuestSeriesEntity
                {
                    Id = series.Id,
                    AccountId = account.Id,
                    QuestDefinitionId = batch.All<QuestDefinitionRevisionEntity>().Single(r => r.Id == termId).QuestDefinitionId,
                    Revision = revision.Revision,
                    NextSequence = series.NextSequence,
                    CreationOrdinal = aggregate.Schedule.Series.IndexOf(series) + 1,
                    PauseDays = series.PauseDays,
                    NextDeliveryOn = next,
                    StoppedAt = series.Stopped ? prior?.StoppedAt ?? recordedAt : null,
                    CreatedAt = now,
                    ModifiedAt = now
                });
            }
        }

        private void Occurrences()
        {
            foreach (var view in state.Occurrences)
            {
                var occurrence = view.Occurrence;
                var life = occurrence.Lifecycle ?? new();
                var termId = Terms(occurrence.Quest);
                var term = batch.All<QuestDefinitionRevisionEntity>().Single(r => r.Id == termId);
                var prior = batch.Original<QuestOccurrenceEntity>().SingleOrDefault(o => o.Id == occurrence.Id);

                // Definition editing runs after pre-command delivery. Those newly materialized
                // occurrences originated under the previous configuration even if their active
                // terms are then edited. Later deliveries use the newest internal configuration.
                var configurations = command.Action == QuestActions.SaveDefinition
                    ? batch.Original<QuestSeriesRevisionEntity>() : batch.All<QuestSeriesRevisionEntity>();
                var seriesRevision = life.SeriesId is null ? null : configurations
                    .Where(r => r.QuestSeriesId == life.SeriesId && r.ScheduleVersion == life.ScheduleVersion).OrderByDescending(r => r.Revision).First();
                var row = new QuestOccurrenceEntity
                {
                    Id = occurrence.Id,
                    AccountId = account.Id,
                    QuestDefinitionId = term.QuestDefinitionId,
                    QuestDefinitionRevisionId = termId,
                    CategoryId = occurrence.Quest.CategoryId,
                    DueOn = Date(occurrence.DueDate),
                    PlannedTime = Time(occurrence.PlannedTime),
                    DeadlineAt = occurrence.Deadline,
                    DeadlineTimeZoneId = life.DeadlineZone ?? aggregate.Zone,
                    StateCode = view.Status.ToString(),
                    AcceptedAt = life.Unaccepted ? null : occurrence.AcceptedAt,
                    OriginatedAt = occurrence.AcceptedAt,
                    OriginatedOffsetMinutes = checked((short)occurrence.AcceptedAt.Offset.TotalMinutes),
                    CreationOrdinal = aggregate.Occurrences.IndexOf(occurrence) + 1,
                    FrozenAt = life.FrozenAt,
                    IsIndividuallyFrozen = life.IndividuallyFrozen,
                    AbandonedAt = life.AbandonedAt,
                    LockedLoss = life.LockedLoss,
                    LossCategoryId = life.LossCategoryId,
                    Revision = prior?.Revision ?? 1,
                    QuestSeriesId = life.SeriesId,
                    QuestSeriesRevisionId = prior?.QuestSeriesRevisionId ?? seriesRevision?.Id,
                    SeriesSequence = life.Sequence,
                    ParentQuestOccurrenceId = occurrence.ParentId,
                    SourceSyncAnchorId = life.SourceAnchorId,
                    CreatedAt = now,
                    ModifiedAt = now
                };
                if (prior is not null && MutationBatch.Same(prior, row))
                {
                    _occurrenceRevisions[row.Id] = batch.Original<QuestOccurrenceRevisionEntity>().Single(r => r.QuestOccurrenceId == row.Id && r.Revision == row.Revision).Id;
                    continue;
                }

                row.Revision = (prior?.Revision ?? 0) + 1;
                batch.Put(row);
                var revisionId = Derived(account.Id, $"occurrence/{row.Id:D}/revision/{row.Revision}");
                _occurrenceRevisions[row.Id] = revisionId;
                batch.Put(new QuestOccurrenceRevisionEntity
                {
                    Id = revisionId,
                    AccountId = account.Id,
                    QuestOccurrenceId = row.Id,
                    Revision = row.Revision,
                    QuestDefinitionId = row.QuestDefinitionId,
                    QuestDefinitionRevisionId = row.QuestDefinitionRevisionId,
                    CategoryId = row.CategoryId,
                    DueOn = row.DueOn,
                    PlannedTime = row.PlannedTime,
                    DeadlineAt = row.DeadlineAt,
                    DeadlineTimeZoneId = row.DeadlineTimeZoneId,
                    StateCode = row.StateCode,
                    AcceptedAt = row.AcceptedAt,
                    OriginatedAt = row.OriginatedAt,
                    OriginatedOffsetMinutes = row.OriginatedOffsetMinutes,
                    FrozenAt = row.FrozenAt,
                    IsIndividuallyFrozen = row.IsIndividuallyFrozen,
                    AbandonedAt = row.AbandonedAt,
                    LockedLoss = row.LockedLoss,
                    LossCategoryId = row.LossCategoryId,
                    AccountMutationVersion = Version,
                    EffectiveAt = recordedAt,
                    CommandReceiptId = ReceiptId,
                    CreatedAt = now
                });
                var code = prior is null ? life.Unaccepted ? "Offered" : "Accepted"
                    : life.AbandonedAt != prior.AbandonedAt ? "Abandoned"
                    : life.FrozenAt is not null && life.FrozenAt != prior.FrozenAt ? "Frozen"
                    : prior.FrozenAt is not null && life.FrozenAt is null ? "Resumed" : "Edited";
                var at = prior is null ? occurrence.AcceptedAt : code == "Abandoned" ? life.AbandonedAt!.Value : code == "Frozen" ? life.FrozenAt!.Value : recordedAt;
                Event(Derived(account.Id, $"transition/{command.OperationId:D}/{row.Id:D}"), row.Id, code, at);
            }
        }

        private void Event(Guid id, Guid occurrenceId, string code, DateTimeOffset at)
        {
            if (batch.All<QuestOccurrenceEventEntity>().Any(e => e.Id == id))
                return;
            batch.Put(new QuestOccurrenceEventEntity
            {
                Id = id,
                AccountId = account.Id,
                QuestOccurrenceId = occurrenceId,
                QuestOccurrenceRevisionId = _occurrenceRevisions[occurrenceId],
                EventCode = code,
                EffectiveAt = at,
                AccountMutationVersion = Version,
                CommandReceiptId = ReceiptId,
                CreatedAt = now
            });
        }

        private void Completions()
        {
            foreach (var completion in aggregate.Completions)
            {
                var prior = batch.Original<QuestCompletionEntity>().SingleOrDefault(c => c.Id == completion.Id);
                if (prior is null)
                {
                    Event(completion.Id, completion.OccurrenceId, "Completed", completion.RecordedAt);
                    prior = new()
                    {
                        Id = completion.Id,
                        AccountId = account.Id,
                        QuestOccurrenceId = completion.OccurrenceId,
                        QuestOccurrenceRevisionId = _occurrenceRevisions[completion.OccurrenceId],
                        RecordedAt = completion.RecordedAt,
                        CreatedAt = now,
                        ModifiedAt = now
                    };
                }
                else
                {
                    prior = MutationBatch.Copy(prior);
                }

                var undo = aggregate.Undos.SingleOrDefault(u => u.CompletionId == completion.Id);
                if (undo is not null)
                {
                    Event(undo.Id, completion.OccurrenceId, "Undone", undo.RecordedAt);
                    prior.UndoneAt = undo.RecordedAt;
                    prior.UndoQuestOccurrenceEventId = undo.Id;
                    prior.ModifiedAt = now;
                }

                batch.Put(prior);
            }
        }

        private void Projections()
        {
            foreach (var entry in state.Ledger)
            {
                var completion = aggregate.Completions.SingleOrDefault(c => c.Id == entry.EventId);
                var eventId = entry.EventId;
                if (completion is null)
                {
                    eventId = Derived(account.Id, $"penalty/{entry.OccurrenceId:D}/{entry.At:O}");
                    Event(eventId, entry.OccurrenceId, "DeadlineElapsed", entry.At);
                }

                var baseAmount = completion is not null && entry.Track == "Category" ? completion.Snapshot!.BaseXp : entry.Amount;
                Ledger(eventId, completion is null ? "Penalty" : "Base", entry.Track, entry.TrackId, entry.At, baseAmount);
                if (completion is not null && entry.Track == "Category" && entry.Amount != baseAmount)
                    Ledger(eventId, "StreakBonus", entry.Track, entry.TrackId, entry.At, entry.Amount - baseAmount);
            }

            Balance("Overall", "overall", state.OverallXp, state.OverallLevel, 0);
            foreach (var (track, values) in new[] { ("Category", values: state.Categories), ("Attribute", values: state.Attributes), ("Skill", values: state.Skills) })
                foreach (var balance in values)
                {
                    var assessment = aggregate.Profile.AssessmentHistory?.Where(a => a.SkillId == balance.Id && a.At <= projectionAt).OrderBy(a => a.At).LastOrDefault();
                    var seed = track == "Skill" && assessment is not null ? Progression.Seed(assessment.Experience) : 0;
                    Balance(track, balance.Id, balance.Xp - seed, balance.Level, seed);
                }

            foreach (var streak in state.Streaks)
            {
                batch.Put(new CategoryProgressEntity
                {
                    Id = Derived(account.Id, "streak/" + streak.CategoryId),
                    AccountId = account.Id,
                    CategoryId = streak.CategoryId,
                    StreakDays = streak.Days,
                    BonusRatePercent = streak.Rate,
                    HasQualifiedToday = streak.QualifiedToday,
                    IsExplicitlyPaused = aggregate.Schedule.PausedCategories.Contains(streak.CategoryId),
                    AsOfDate = Date(state.Today)!.Value,
                    TimeZoneId = aggregate.Zone,
                    ProjectionVersion = Version,
                    CreatedAt = now,
                    ModifiedAt = now
                });
            }

            foreach (var entitlement in state.Entitlements)
            {
                batch.Put(new AccountEntitlementEntity
                {
                    Id = Derived(account.Id, "reward/" + entitlement.Id),
                    AccountId = account.Id,
                    ProfileRewardId = entitlement.Id,
                    QualifyingCount = entitlement.Count,
                    IsEarned = entitlement.Earned,
                    ProjectionVersion = Version,
                    RulesetVersion = "1.0",
                    CreatedAt = now,
                    ModifiedAt = now
                });
            }
        }

        private void Ledger(Guid eventId, string contribution, string track, string target, DateTimeOffset at, long amount)
        {
            var (system, custom) = track == "Skill" ? Skill(target) : default;
            batch.Put(new XpLedgerEntryEntity
            {
                Id = Derived(account.Id, $"ledger/{eventId:D}/{contribution}/{track}/{target}"),
                AccountId = account.Id,
                QuestOccurrenceEventId = eventId,
                ContributionCode = contribution,
                TrackCode = track,
                CategoryId = track == "Category" ? target : null,
                AttributeId = track == "Attribute" ? target : null,
                SystemSkillId = system,
                CustomSkillId = custom,
                EffectiveAt = at,
                Amount = amount,
                ProjectionVersion = Version,
                RulesetVersion = "1.0",
                CreatedAt = now,
                ModifiedAt = now
            });
        }

        private void Balance(string track, string target, long earned, int level, long seed)
        {
            var (system, custom) = track == "Skill" ? Skill(target) : default;
            batch.Put(new XpBalanceEntity
            {
                Id = Derived(account.Id, $"balance/{track}/{target}"),
                AccountId = account.Id,
                TrackCode = track,
                CategoryId = track == "Category" ? target : null,
                AttributeId = track == "Attribute" ? target : null,
                SystemSkillId = system,
                CustomSkillId = custom,
                EarnedXp = earned,
                SeedXp = seed,
                Level = level,
                ProjectionVersion = Version,
                CreatedAt = now,
                ModifiedAt = now
            });
        }

        private void History()
        {
            var action = command.Action;
            var targetOccurrence = action is QuestActions.Accept ? aggregate.Occurrences[^1] : command.OccurrenceId is { } id ? aggregate.Occurrences.SingleOrDefault(o => o.Id == id) : null;
            Quest? quest = action switch
            {
                QuestActions.SaveDefinition => aggregate.Definitions.Single(d => d.Quest.Id == command.Definition!.Id).Quest,
                QuestActions.ArchiveDefinition => aggregate.Definitions.Single(d => d.Quest.Id == command.QuestId).Quest,
                QuestActions.SaveSeries => aggregate.Schedule.Series.Single(s => s.Id == command.SeriesId).Quest,
                QuestActions.Accept or QuestActions.AcceptOffer or QuestActions.Abandon => targetOccurrence?.Quest,
                _ => null,
            };
            var (system, custom) = action is QuestActions.SaveSkill or QuestActions.ArchiveSkill or QuestActions.AssessSkill ? Skill(command.SkillId!) : default;
            var history = new QuestCommandHistoryEntity
            {
                Id = command.OperationId,
                AccountId = account.Id,
                CommandReceiptId = ReceiptId,
                AccountMutationVersion = Version,
                ReconciliationLimit = reconciliationLimit,
                ActionReconciliationLimit = actionReconciliationLimit,
                ActionCode = action,
                RulesetVersion = "1.0",
                TimeZoneBefore = account.TimeZoneId,
                ReconciledAt = reconciledAt,
                RecordedAt = recordedAt,
                ProjectionAt = projectionAt,
                QuestOccurrenceId = action is QuestActions.Accept or QuestActions.Complete or QuestActions.Undo or QuestActions.Plan or QuestActions.Link
                    or QuestActions.ResumeOccurrence or QuestActions.Abandon or QuestActions.AcceptOffer ? targetOccurrence?.Id : null,
                QuestCompletionId = action == QuestActions.Undo ? command.CompletionId : null,
                QuestDefinitionRevisionId = quest is null ? null : Terms(quest),
                CompletionTermsRevisionId = action == QuestActions.Complete && command.RecordedTime is not null ? _occurrenceRevisions[targetOccurrence!.Id] : null,
                ExpectedRevision = action is QuestActions.SaveDefinition or QuestActions.ArchiveDefinition or QuestActions.SaveSkill or QuestActions.ArchiveSkill or QuestActions.SaveSeries ? command.ExpectedRevision : null,
                SystemSkillId = system,
                CustomSkillId = custom,
                SkillName = action == QuestActions.SaveSkill ? aggregate.Profile.CustomSkills!.Single(s => s.Id == command.SkillId).Name : null,
                ExperienceCode = action == QuestActions.AssessSkill ? command.Experience!.Value.ToString() : null,
                DueOn = action is QuestActions.Accept or QuestActions.Plan or QuestActions.SaveSeries ? Date(command.DueDate) : null,
                PlannedTime = action is QuestActions.Accept or QuestActions.Plan or QuestActions.SaveSeries ? Time(command.PlannedTime) : null,
                ParentQuestOccurrenceId = action == QuestActions.Link ? command.ParentId : null,
                ProfileRewardId = action is QuestActions.SelectBadge or QuestActions.SelectFrame ? command.RewardId : null,
                QuestSeriesId = action is QuestActions.SaveSeries or QuestActions.StopSeries ? command.SeriesId : null,
                CadenceCode = action == QuestActions.SaveSeries ? command.Cadence!.Value.ToString() : null,
                Interval = action == QuestActions.SaveSeries ? command.Interval : null,
                CategoryId = action is QuestActions.Pause or QuestActions.Resume ? command.CategoryId : null,
                ConfirmPenalty = command.ConfirmPenalty,
                AcceptedLoss = command.AcceptedLoss,
                HasAcceptedTerms = command.AcceptedQuest is not null && quest is not null,
                NewTimeZone = action == QuestActions.Zone ? command.NewZone : null,
                ExpectedTimeZone = action == QuestActions.Zone ? command.ExpectedZone : null,
                ConfirmZoneChange = command.ConfirmZoneChange,
                HasExpiryWarnings = action == QuestActions.ExpiryWarnings ? command.ExpiryWarnings : null,
                SourceSyncAnchorId = command.RecordedTime?.AnchorId,
                CreatedAt = now
            };
            batch.Put(history);
            if (action == QuestActions.Interests)
            {
                for (var index = 0; index < aggregate.Profile.Interests.Length; index++)
                {
                    batch.Put(new QuestCommandInterestEntity
                    {
                        AccountId = account.Id,
                        QuestCommandHistoryId = command.OperationId,
                        CategoryId = aggregate.Profile.Interests[index],
                        Position = index,
                        CreatedAt = now
                    });
                }
            }
        }
    }
}
