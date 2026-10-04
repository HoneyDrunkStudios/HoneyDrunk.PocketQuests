-- Typed transport rows only. These types do not persist documents or account snapshots.
CREATE TYPE [pocketquests].[AllocationInput] AS TABLE (
    [TargetId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY,
    [BasisPoints] int NOT NULL CHECK ([BasisPoints] >= 0 AND [BasisPoints] <= 10000)
);
GO
CREATE TYPE [pocketquests].[LedgerInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [EventId] uniqueidentifier NOT NULL,
    [ContributionCode] varchar(20) NOT NULL,
    [TrackCode] varchar(20) NOT NULL,
    [TargetId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [EffectiveAt] datetimeoffset(7) NOT NULL,
    [Amount] bigint NOT NULL
);
GO
CREATE TYPE [pocketquests].[BalanceInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [TrackCode] varchar(20) NOT NULL,
    [TargetId] varchar(40) COLLATE Latin1_General_100_BIN2 NULL,
    [EarnedXp] bigint NOT NULL,
    [Level] int NOT NULL
);
GO
CREATE TYPE [pocketquests].[StreakInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [CategoryId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL UNIQUE,
    [Days] int NOT NULL,
    [Bonus] int NOT NULL,
    [HasQualifiedToday] bit NOT NULL
);
GO
CREATE TYPE [pocketquests].[EntitlementInput] AS TABLE (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [RewardId] varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL UNIQUE,
    [QualifyingCount] int NOT NULL,
    [IsEarned] bit NOT NULL
);
GO
