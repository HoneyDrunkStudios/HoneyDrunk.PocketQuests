CREATE TABLE [dbo].[Occurrences] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [QuestSnapshot] nvarchar(max) NOT NULL,
    [DueDate] varchar(10) NULL,
    [Deadline] datetimeoffset NULL,
    [AcceptedAt] datetimeoffset NOT NULL,
    [PlannedTime] varchar(5) NULL,
    [ParentId] uniqueidentifier NULL,
    [Lifecycle] nvarchar(max) NULL,
    CONSTRAINT [PK_Occurrences] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_Occurrences_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Occurrences_Occurrences_AccountId_ParentId] FOREIGN KEY ([AccountId], [ParentId]) REFERENCES [dbo].[Occurrences] ([AccountId], [Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_Occurrences_AccountId_ParentId] ON [dbo].[Occurrences] ([AccountId], [ParentId]);
GO
