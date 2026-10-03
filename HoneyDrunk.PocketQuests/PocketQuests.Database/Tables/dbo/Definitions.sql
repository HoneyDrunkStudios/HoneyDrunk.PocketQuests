CREATE TABLE [dbo].[Definitions] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] varchar(36) NOT NULL,
    [Document] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Definitions] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_Definitions_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION
);
GO
