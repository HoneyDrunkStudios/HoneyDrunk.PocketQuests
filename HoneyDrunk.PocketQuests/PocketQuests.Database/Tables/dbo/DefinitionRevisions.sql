CREATE TABLE [dbo].[DefinitionRevisions] (
    [AccountId] uniqueidentifier NOT NULL,
    [DefinitionId] varchar(36) NOT NULL,
    [Revision] int NOT NULL,
    [Document] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_DefinitionRevisions] PRIMARY KEY ([AccountId], [DefinitionId], [Revision]),
    CONSTRAINT [FK_DefinitionRevisions_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION
);
GO
