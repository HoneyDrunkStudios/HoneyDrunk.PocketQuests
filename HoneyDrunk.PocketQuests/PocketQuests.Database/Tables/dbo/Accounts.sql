CREATE TABLE [dbo].[Accounts] (
    [Id] uniqueidentifier NOT NULL,
    [IdentityKey] varchar(64) NOT NULL,
    [Zone] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [LastRecordedAt] datetimeoffset NULL,
    [Profile] nvarchar(max) NULL,
    [Schedule] nvarchar(max) NULL,
    CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [IX_Accounts_IdentityKey] ON [dbo].[Accounts] ([IdentityKey]);
GO
