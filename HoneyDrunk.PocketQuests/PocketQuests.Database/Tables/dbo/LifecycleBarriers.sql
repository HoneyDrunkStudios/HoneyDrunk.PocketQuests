CREATE TABLE [dbo].[LifecycleBarriers] (
    [UserId] varchar(30) NOT NULL,
    [Version] bigint NOT NULL,
    [State] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_LifecycleBarriers] PRIMARY KEY ([UserId])
);
GO
