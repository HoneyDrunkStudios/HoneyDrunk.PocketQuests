using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Services.Commands.Mapping;
using PocketQuests.Services.Exports.Mapping;
using PocketQuests.Services.Projections.Mapping;
using PocketQuests.Services.Quests.Validators;
using PocketQuests.Services.Synchronization.Mapping;
using System.Collections.Immutable;

namespace PocketQuests.Tests.Api;

internal sealed class ApiTestStore : PocketQuests.Services.Quests.IQuestService, PocketQuests.Services.Profiles.IProfileService, PocketQuests.Services.Synchronization.ISynchronizationService, PocketQuests.Services.Exports.IExportService
{
    internal static readonly DateTimeOffset At = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    internal Exception? Failure { get; set; }

    internal QuestCommand? LastCommand { get; private set; }

    internal int Commands { get; private set; }

    internal QuestState State { get; } = CreateState();

    public Task<QuestState> Read(AccountIdentity identity, DateTimeOffset now, CancellationToken cancellationToken) =>
        Failure is { } failure ? Task.FromException<QuestState>(failure) : Task.FromResult(State);

    public Task<QuestState> Initialize(AccountIdentity identity, string initialZone, DateTimeOffset now, CancellationToken cancellationToken) => Read(identity, now, cancellationToken);

    public Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Commands++;
        LastCommand = command;
        if (command.Action == "invalid-domain-action")
            new QuestAggregate("UTC").Apply(command, now);
        return Read(identity, now, cancellationToken);
    }

    public Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken cancellationToken) =>
        Failure is { } failure ? Task.FromException<QuestExport>(failure) : Task.FromResult(Snapshot());

    public Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token) =>
        Failure is { } failure ? Task.FromException<SyncAnchor>(failure) : Task.FromResult(new SyncAnchor(Guid.Empty, deviceId, bootId, now, deviceUtc));

    async Task<PocketQuests.Contracts.Responses.Projections.QuestState> PocketQuests.Services.Quests.IQuestService.Execute(PocketQuests.Contracts.Requests.Commands.QuestCommand request, CancellationToken token)
    {
        var errors = QuestValidator.ValidateInput(request);
        if (errors.Count > 0)
            throw new PocketQuests.Domain.Errors.QuestValidationException(string.Join(" ", errors));
        return (await Execute(new("honeydrunk-identity", "usr_00000000000000000000000000"), request.ToModel(), At, token)).ToModel();
    }

    async Task<PocketQuests.Contracts.Responses.Projections.QuestState> PocketQuests.Services.Quests.IQuestService.Read(CancellationToken token) =>
        (await Read(new("honeydrunk-identity", "usr_00000000000000000000000000"), At, token)).ToModel();

    async Task<PocketQuests.Contracts.Responses.Projections.QuestState> PocketQuests.Services.Profiles.IProfileService.Initialize(PocketQuests.Contracts.Requests.Profiles.InitializeProfile request, CancellationToken token) =>
        (await Initialize(new("honeydrunk-identity", "usr_00000000000000000000000000"), request.Zone, At, token)).ToModel();

    async Task<PocketQuests.Contracts.Models.Synchronization.SyncAnchor> PocketQuests.Services.Synchronization.ISynchronizationService.CreateAnchor(PocketQuests.Contracts.Requests.Synchronization.AnchorRequest request, CancellationToken token) =>
        (await CreateAnchor(new("honeydrunk-identity", "usr_00000000000000000000000000"), request.DeviceId, request.BootId, request.DeviceUtc, At, token)).ToModel();

    async Task<PocketQuests.Contracts.Responses.Exports.QuestExport> PocketQuests.Services.Exports.IExportService.Read(CancellationToken token) =>
        (await Export(new("honeydrunk-identity", "usr_00000000000000000000000000"), At, token)).ToModel();

    internal QuestExport Snapshot() => new(1, At, At, Guid.Empty, State, [], [State.Occurrences[0].Completion!], []);

    private static QuestState CreateState()
    {
        var aggregate = new QuestAggregate("UTC");
        var occurrenceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        aggregate.Apply(new(occurrenceId, QuestActions.Accept, OccurrenceId: occurrenceId, QuestId: Catalog.Quests[0].Id), At);
        var before = aggregate.Project(At);
        var completionId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        aggregate.Apply(new(completionId, QuestActions.Complete, OccurrenceId: occurrenceId), At);
        var after = aggregate.Project(At);
        return after with
        {
            CompletionOutcome = CompletionOutcome.Between(before, after, completionId, occurrenceId),
            Definitions = [new(Catalog.Quests[0], 2)],
            Profile = after.Profile with
            {
                Interests = ["c01"],
                Assessments = ImmutableDictionary<string, Experience>.Empty.Add("s01", Experience.Expert),
                CustomSkills = [new("custom", "Synthetic skill")],
                AssessmentHistory = [new("s01", Experience.Expert, At)],
                ZoneHistory = [new("UTC", "Europe/London", At)],
            },
            Schedule = new([new(Guid.Empty, Catalog.Quests[0], "2026-10-04", Cadence.Weeks, 1)], [new("c01", At)], ["c01"]),
            FutureWarnings = [At],
            Penalties = [new(occurrenceId, "c01", 1, 1, At)],
        };
    }
}
