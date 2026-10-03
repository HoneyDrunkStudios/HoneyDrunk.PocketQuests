CREATE TABLE [dbo].[SyncAnchors] (
    [AccountId] uniqueidentifier NOT NULL,
    [Id] uniqueidentifier NOT NULL,
    [DeviceId] uniqueidentifier NOT NULL,
    [BootId] uniqueidentifier NOT NULL,
    [ServerUtc] datetimeoffset NOT NULL,
    [RecordedTimeFloor] datetimeoffset NULL,
    [DeviceUtc] datetimeoffset NOT NULL,
    [LastOrdinal] bigint NOT NULL,
    [LastElapsedMilliseconds] float NOT NULL,
    [Snapshot] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_SyncAnchors] PRIMARY KEY ([AccountId], [Id]),
    CONSTRAINT [FK_SyncAnchors_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION
);
GO
