CREATE TABLE [dbo].[Completions] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [OccurrenceId] uniqueidentifier NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [QuestSnapshot] nvarchar(max) NULL,
    CONSTRAINT [PK_Completions] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_Completions_Occurrences_AccountId_OccurrenceId] FOREIGN KEY ([AccountId], [OccurrenceId]) REFERENCES [dbo].[Occurrences] ([AccountId], [Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_Completions_AccountId_OccurrenceId] ON [dbo].[Completions] ([AccountId], [OccurrenceId]);
GO
