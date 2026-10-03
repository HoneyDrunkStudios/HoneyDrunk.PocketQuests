CREATE TABLE [dbo].[Operations] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [Result] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Operations] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_Operations_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION
);
GO
