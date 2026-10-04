-- These procedures belong only to the private, verified Identity lifecycle/restore service.
CREATE PROCEDURE [pocketquests].[SetLifecycleState]
    @IdentityUserId varchar(30),@ExpectedVersion bigint,@Version bigint,@State varchar(8),
    @EffectiveAt datetimeoffset(7),@PausedAt datetimeoffset(7),@Now datetimeoffset(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    EXEC [pocketquests].[AcquireAccountLock] @IdentityUserId;
    IF @Version IS NULL OR @Version<=0 OR @ExpectedVersion IS NULL OR @Version<=@ExpectedVersion
       OR @State IS NULL OR @State NOT IN ('Active','Inactive') OR @EffectiveAt IS NULL OR @PausedAt IS NULL OR @Now IS NULL
       OR @PausedAt>@EffectiveAt OR @EffectiveAt>DATEADD(second,30,@Now)
        THROW 51301, 'Invalid verified lifecycle transition.', 1;
    IF EXISTS(SELECT 1 FROM [pocketquests].[ErasureMarker] WHERE Id=@IdentityUserId)
        THROW 51103, 'Account was erased.', 1;
    DECLARE @prior bigint=0,@account uniqueidentifier;
    SELECT @prior=Version FROM [pocketquests].[AccountLifecycleState] WHERE IdentityUserId=@IdentityUserId;
    IF @prior<>@ExpectedVersion THROW 51302, 'Lifecycle version changed.', 1;
    SELECT @account=Id FROM [pocketquests].[Account] WHERE IdentityUserId=@IdentityUserId;
    IF @prior=0 AND NOT EXISTS(SELECT 1 FROM [pocketquests].[AccountLifecycleState] WHERE IdentityUserId=@IdentityUserId)
        INSERT [pocketquests].[AccountLifecycleState](Id,IdentityUserId,AccountId,Version,StateCode,EffectiveAt,PausedAt,CreatedAt,ModifiedAt)
            VALUES(NEWID(),@IdentityUserId,@account,@Version,@State,@EffectiveAt,@PausedAt,@Now,@Now);
    ELSE
        UPDATE [pocketquests].[AccountLifecycleState] SET AccountId=@account,Version=@Version,StateCode=@State,
            EffectiveAt=@EffectiveAt,PausedAt=@PausedAt,ModifiedAt=CASE WHEN ModifiedAt>@Now THEN ModifiedAt ELSE @Now END
            WHERE IdentityUserId=@IdentityUserId;
    UPDATE [pocketquests].[SyncAnchor] SET InvalidatedAt=@Now,
        ModifiedAt=CASE WHEN ModifiedAt>@Now THEN ModifiedAt ELSE @Now END
        WHERE AccountId=@account AND InvalidatedAt IS NULL;
END;
GO
CREATE PROCEDURE [pocketquests].[StageLifecycleAcknowledgment]
    @IdentityUserId varchar(30),@Version bigint,@Id uniqueidentifier,@Type nvarchar(512),@Payload nvarchar(max),
    @Headers nvarchar(max),@ExpiresAt datetimeoffset(7),@Now datetimeoffset(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    EXEC [pocketquests].[AcquireAccountLock] @IdentityUserId;
    IF @Version IS NULL OR @Version<=0 OR @Id IS NULL OR @Type IS NULL OR @Payload IS NULL OR ISJSON(@Payload)<>1
       OR @Headers IS NULL OR ISJSON(@Headers)<>1 OR @Now IS NULL OR @ExpiresAt IS NULL
       OR @ExpiresAt<=@Now OR @ExpiresAt>DATEADD(hour,1,@Now)
        THROW 51303, 'Invalid lifecycle acknowledgment envelope.', 1;
    IF EXISTS(SELECT 1 FROM [pocketquests].[LifecycleMessage] WHERE Id=@Id)
    BEGIN
        IF NOT EXISTS(SELECT 1 FROM [pocketquests].[LifecycleMessage] WHERE Id=@Id AND IdentityUserId=@IdentityUserId AND LifecycleVersion=@Version)
            THROW 51303, 'Acknowledgment identifier belongs to another owner or version.', 1;
        RETURN;
    END;
    DECLARE @account uniqueidentifier;
    SELECT @account=Id FROM [pocketquests].[Account] WHERE IdentityUserId=@IdentityUserId;
    INSERT [outbox].[OutboxMessages](Id,Type,Payload,OccurredAt,Headers,TenantId,CorrelationId,Status,RetryCount)
        VALUES(@Id,@Type,@Payload,@Now,@Headers,N'internal',CONVERT(nvarchar(36),@Id),0,0);
    INSERT [pocketquests].[LifecycleMessage](Id,IdentityUserId,AccountId,LifecycleVersion,OutboxMessageId,ExpiresAt,CreatedAt)
        VALUES(@Id,@IdentityUserId,@account,@Version,@Id,@ExpiresAt,@Now);
END;
GO
CREATE PROCEDURE [pocketquests].[PruneLifecycle]
    @Now datetimeoffset(7),@DispatchedStatus int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT=0 OR @Now IS NULL OR @DispatchedStatus IS NULL THROW 51304, 'Retention requires a transaction and explicit clock.', 1;
    DECLARE @outbox TABLE(Id uniqueidentifier PRIMARY KEY);
    DELETE m OUTPUT deleted.OutboxMessageId INTO @outbox
        FROM [pocketquests].[LifecycleMessage] m JOIN [outbox].[OutboxMessages] o ON o.Id=m.OutboxMessageId
        WHERE m.ExpiresAt<=@Now OR o.Status=@DispatchedStatus;
    DELETE o FROM [outbox].[OutboxMessages] o JOIN @outbox d ON d.Id=o.Id;
    DELETE [pocketquests].[ErasureMarker] WHERE CreatedAt<=DATEADD(day,-35,@Now);
END;
GO
