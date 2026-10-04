CREATE TYPE [pocketquests].[AccountMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [IdentityUserId] varchar(30) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [TimeZoneId] varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [IsOnboardingComplete] bit NOT NULL,
    [HasExpiryWarnings] bit NOT NULL,
    [IsAccountPaused] bit NOT NULL,
    [SelectedBadgeId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [SelectedBadgeKind] varchar(12) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [SelectedFrameId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [SelectedFrameKind] varchar(12) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [LastRecordedAt] datetimeoffset(7) NOT NULL,
    [MutationVersion] bigint NOT NULL,
    [ProjectionVersion] bigint NOT NULL,
    [ProjectionAsOfAt] datetimeoffset(7) NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    [HasPendingReconciliation] bit NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[CustomSkillMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [Name] nvarchar(80) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [NormalizedName] nvarchar(80) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [NameNormalizationVersion] smallint NOT NULL,
    [Revision] int NOT NULL,
    [ArchivedAt] datetimeoffset(7) NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    [CreationOrdinal] int NOT NULL,
    [ClientKey] varchar(36) COLLATE Latin1_General_100_BIN2 NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestDefinitionMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [SystemQuestId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [Revision] int NOT NULL,
    [ArchivedAt] datetimeoffset(7) NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    [CreationOrdinal] int NOT NULL,
    [ClientKey] varchar(36) COLLATE Latin1_General_100_BIN2 NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestDefinitionRevisionMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [QuestDefinitionId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [Title] nvarchar(120) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Criterion] nvarchar(2000) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Description] nvarchar(2000) COLLATE Latin1_General_100_BIN2 NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [RankCode] char(1) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [EffortCode] varchar(6) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [BaseXp] bigint NOT NULL,
    [PenaltyPercent] tinyint NOT NULL,
    [RulesetVersion] varchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [DisplaySnapshotVersion] smallint NOT NULL,
    [DisplaySnapshotJson] nvarchar(max) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [EffectiveAt] datetimeoffset(7) NOT NULL,
    [CommandReceiptId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestDefinitionAttributeAllocationMutationInput] AS TABLE (
    [AccountId] uniqueidentifier NOT NULL,
    [QuestDefinitionRevisionId] uniqueidentifier NOT NULL,
    [AttributeId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [BasisPoints] int NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [Position] int NOT NULL,
    PRIMARY KEY ([AccountId],[QuestDefinitionRevisionId],[AttributeId])
);
GO
CREATE TYPE [pocketquests].[QuestDefinitionSkillAllocationMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [QuestDefinitionRevisionId] uniqueidentifier NOT NULL,
    [SystemSkillId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [CustomSkillId] uniqueidentifier NULL,
    [BasisPoints] int NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [Position] int NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestSeriesMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [QuestDefinitionId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [NextSequence] int NOT NULL,
    [PauseDays] int NOT NULL,
    [NextDeliveryOn] date NULL,
    [StoppedAt] datetimeoffset(7) NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    [CreationOrdinal] int NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestSeriesRevisionMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [QuestSeriesId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [QuestDefinitionRevisionId] uniqueidentifier NOT NULL,
    [CadenceCode] varchar(6) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Interval] int NOT NULL,
    [AnchorOn] date NOT NULL,
    [PlannedTime] time(0) NULL,
    [HasAutoAcceptPenalty] bit NOT NULL,
    [EffectiveAt] datetimeoffset(7) NOT NULL,
    [CommandReceiptId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ScheduleVersion] int NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestOccurrenceMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [QuestDefinitionId] uniqueidentifier NOT NULL,
    [QuestDefinitionRevisionId] uniqueidentifier NOT NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [DueOn] date NULL,
    [PlannedTime] time(0) NULL,
    [DeadlineAt] datetimeoffset(7) NULL,
    [DeadlineTimeZoneId] varchar(100) COLLATE Latin1_General_100_BIN2 NULL,
    [StateCode] varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [AcceptedAt] datetimeoffset(7) NULL,
    [FrozenAt] datetimeoffset(7) NULL,
    [IsIndividuallyFrozen] bit NOT NULL,
    [AbandonedAt] datetimeoffset(7) NULL,
    [LockedLoss] bigint NULL,
    [LossCategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [Revision] int NOT NULL,
    [QuestSeriesId] uniqueidentifier NULL,
    [QuestSeriesRevisionId] uniqueidentifier NULL,
    [SeriesSequence] int NULL,
    [ParentQuestOccurrenceId] uniqueidentifier NULL,
    [SourceSyncAnchorId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    [OriginatedAt] datetimeoffset(7) NULL,
    [CreationOrdinal] int NOT NULL,
    [OriginatedOffsetMinutes] smallint NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestOccurrenceRevisionMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [QuestOccurrenceId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [QuestDefinitionId] uniqueidentifier NOT NULL,
    [QuestDefinitionRevisionId] uniqueidentifier NOT NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [DueOn] date NULL,
    [PlannedTime] time(0) NULL,
    [DeadlineAt] datetimeoffset(7) NULL,
    [DeadlineTimeZoneId] varchar(100) COLLATE Latin1_General_100_BIN2 NULL,
    [StateCode] varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [AcceptedAt] datetimeoffset(7) NULL,
    [FrozenAt] datetimeoffset(7) NULL,
    [IsIndividuallyFrozen] bit NOT NULL,
    [AbandonedAt] datetimeoffset(7) NULL,
    [LockedLoss] bigint NULL,
    [LossCategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [AccountMutationVersion] bigint NOT NULL,
    [EffectiveAt] datetimeoffset(7) NOT NULL,
    [CommandReceiptId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [OriginatedAt] datetimeoffset(7) NULL,
    [OriginatedOffsetMinutes] smallint NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestOccurrenceEventMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [QuestOccurrenceId] uniqueidentifier NOT NULL,
    [QuestOccurrenceRevisionId] uniqueidentifier NOT NULL,
    [EventCode] varchar(20) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [EffectiveAt] datetimeoffset(7) NOT NULL,
    [AccountMutationVersion] bigint NOT NULL,
    [CommandReceiptId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestCompletionMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [QuestOccurrenceId] uniqueidentifier NOT NULL,
    [QuestOccurrenceRevisionId] uniqueidentifier NOT NULL,
    [RecordedAt] datetimeoffset(7) NOT NULL,
    [UndoneAt] datetimeoffset(7) NULL,
    [UndoQuestOccurrenceEventId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[AccountPauseMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [ScopeCode] varchar(12) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [StartedAt] datetimeoffset(7) NOT NULL,
    [EndedAt] datetimeoffset(7) NULL,
    [CommandReceiptId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    [CreationOrdinal] int NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[TimeZoneChangeMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [FromTimeZoneId] varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ToTimeZoneId] varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [EffectiveAt] datetimeoffset(7) NOT NULL,
    [CommandReceiptId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[SkillAssessmentMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [SystemSkillId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [CustomSkillId] uniqueidentifier NULL,
    [ExperienceCode] varchar(12) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [SeedXp] bigint NOT NULL,
    [RulesetVersion] varchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [EffectiveAt] datetimeoffset(7) NOT NULL,
    [CommandReceiptId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestCommandHistoryMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [CommandReceiptId] uniqueidentifier NULL,
    [AccountMutationVersion] bigint NOT NULL,
    [ActionCode] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [RulesetVersion] varchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [TimeZoneBefore] varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ReconciledAt] datetimeoffset(7) NOT NULL,
    [RecordedAt] datetimeoffset(7) NOT NULL,
    [ProjectionAt] datetimeoffset(7) NOT NULL,
    [QuestOccurrenceId] uniqueidentifier NULL,
    [QuestCompletionId] uniqueidentifier NULL,
    [QuestDefinitionRevisionId] uniqueidentifier NULL,
    [CompletionTermsRevisionId] uniqueidentifier NULL,
    [ExpectedRevision] int NULL,
    [SystemSkillId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [CustomSkillId] uniqueidentifier NULL,
    [SkillName] nvarchar(80) COLLATE DATABASE_DEFAULT NULL,
    [ExperienceCode] varchar(12) COLLATE Latin1_General_100_BIN2 NULL,
    [DueOn] date NULL,
    [PlannedTime] time(0) NULL,
    [ParentQuestOccurrenceId] uniqueidentifier NULL,
    [ProfileRewardId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [QuestSeriesId] uniqueidentifier NULL,
    [CadenceCode] varchar(6) COLLATE Latin1_General_100_BIN2 NULL,
    [Interval] int NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [ConfirmPenalty] bit NOT NULL,
    [AcceptedLoss] int NULL,
    [HasAcceptedTerms] bit NOT NULL,
    [NewTimeZone] varchar(100) COLLATE Latin1_General_100_BIN2 NULL,
    [ExpectedTimeZone] varchar(100) COLLATE Latin1_General_100_BIN2 NULL,
    [ConfirmZoneChange] bit NOT NULL,
    [HasExpiryWarnings] bit NULL,
    [SourceSyncAnchorId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ReconciliationLimit] int NOT NULL,
    [ActionReconciliationLimit] int NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[QuestCommandInterestMutationInput] AS TABLE (
    [AccountId] uniqueidentifier NOT NULL,
    [QuestCommandHistoryId] uniqueidentifier NOT NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Position] int NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([AccountId],[QuestCommandHistoryId],[CategoryId])
);
GO
CREATE TYPE [pocketquests].[AccountInterestMutationInput] AS TABLE (
    [AccountId] uniqueidentifier NOT NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [Position] int NOT NULL,
    PRIMARY KEY ([AccountId],[CategoryId])
);
GO
CREATE TYPE [pocketquests].[XpLedgerEntryMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [QuestOccurrenceEventId] uniqueidentifier NOT NULL,
    [ContributionCode] varchar(12) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [TrackCode] varchar(12) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [AttributeId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [SystemSkillId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [CustomSkillId] uniqueidentifier NULL,
    [EffectiveAt] datetimeoffset(7) NOT NULL,
    [Amount] bigint NOT NULL,
    [ProjectionVersion] bigint NOT NULL,
    [RulesetVersion] varchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[XpBalanceMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [TrackCode] varchar(12) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [AttributeId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [SystemSkillId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [CustomSkillId] uniqueidentifier NULL,
    [EarnedXp] bigint NOT NULL,
    [SeedXp] bigint NOT NULL,
    [Level] int NOT NULL,
    [ProjectionVersion] bigint NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[CategoryProgressMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [StreakDays] int NOT NULL,
    [BonusRatePercent] int NOT NULL,
    [HasQualifiedToday] bit NOT NULL,
    [IsExplicitlyPaused] bit NOT NULL,
    [AsOfDate] date NOT NULL,
    [TimeZoneId] varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ProjectionVersion] bigint NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([Id])
);
GO
CREATE TYPE [pocketquests].[AccountEntitlementMutationInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [ProfileRewardId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [QualifyingCount] int NOT NULL,
    [IsEarned] bit NOT NULL,
    [ProjectionVersion] bigint NOT NULL,
    [RulesetVersion] varchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ModifiedAt] datetimeoffset(7) NOT NULL,
    PRIMARY KEY ([Id])
);
GO
