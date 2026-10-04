CREATE PROCEDURE [pocketquests].[IssueSyncAnchor]
    @IdentityUserId varchar(30), @Id uniqueidentifier, @DeviceId uniqueidentifier,
    @BootId uniqueidentifier, @DeviceAt datetimeoffset(7), @Now datetimeoffset(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    EXEC [pocketquests].[LockAccount] @IdentityUserId;
    DECLARE @account uniqueidentifier, @version bigint, @floor datetimeoffset(7);
    SELECT @account=Id, @version=MutationVersion, @floor=LastRecordedAt FROM [pocketquests].[Account] WHERE IdentityUserId=@IdentityUserId;
    IF @account IS NULL THROW 51104, 'Account was not initialized.', 1;
    IF @floor < @Now SET @floor=@Now;
    IF @floor > DATEADD(second,5,@Now) THROW 51105, 'Server clock is behind committed history.', 1;
    IF @DeviceId='00000000-0000-0000-0000-000000000000' OR @BootId='00000000-0000-0000-0000-000000000000'
        THROW 51106, 'Device and process identifiers are required.', 1;
    INSERT [pocketquests].[SyncAnchor]
        (Id,AccountId,DeviceId,BootId,ServerAt,DeviceAt,RecordedTimeFloorAt,IssuedMutationVersion,CreatedAt,ModifiedAt)
    VALUES (@Id,@account,@DeviceId,@BootId,@Now,@DeviceAt,@floor,@version,@Now,@Now);
END;
GO
