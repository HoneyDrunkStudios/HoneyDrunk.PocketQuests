-- Frozen legacy schema used only to verify DACPAC upgrades.
CREATE TABLE [Accounts] (
    [Id] uniqueidentifier NOT NULL,
    [IdentityKey] varchar(64) NOT NULL,
    [Zone] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Occurrences] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [QuestSnapshot] nvarchar(max) NOT NULL,
    [DueDate] varchar(10) NULL,
    [Deadline] datetimeoffset NULL,
    [AcceptedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Occurrences] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_Occurrences_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Operations] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [Result] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Operations] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_Operations_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Completions] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [OccurrenceId] uniqueidentifier NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Completions] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_Completions_Occurrences_AccountId_OccurrenceId] FOREIGN KEY ([AccountId], [OccurrenceId]) REFERENCES [Occurrences] ([AccountId], [Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Undos] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [CompletionId] uniqueidentifier NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Undos] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_Undos_Completions_AccountId_CompletionId] FOREIGN KEY ([AccountId], [CompletionId]) REFERENCES [Completions] ([AccountId], [Id]) ON DELETE NO ACTION
);
GO

CREATE UNIQUE INDEX [IX_Accounts_IdentityKey] ON [Accounts] ([IdentityKey]);
GO

CREATE INDEX [IX_Completions_AccountId_OccurrenceId] ON [Completions] ([AccountId], [OccurrenceId]);
GO

CREATE UNIQUE INDEX [IX_Undos_AccountId_CompletionId] ON [Undos] ([AccountId], [CompletionId]);
