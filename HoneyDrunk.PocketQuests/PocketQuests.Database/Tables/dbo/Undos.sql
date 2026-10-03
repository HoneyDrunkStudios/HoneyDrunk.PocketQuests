CREATE TABLE [dbo].[Undos] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [CompletionId] uniqueidentifier NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Undos] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_Undos_Completions_AccountId_CompletionId] FOREIGN KEY ([AccountId], [CompletionId]) REFERENCES [dbo].[Completions] ([AccountId], [Id]) ON DELETE NO ACTION
);
GO

CREATE UNIQUE INDEX [IX_Undos_AccountId_CompletionId] ON [dbo].[Undos] ([AccountId], [CompletionId]);
GO
