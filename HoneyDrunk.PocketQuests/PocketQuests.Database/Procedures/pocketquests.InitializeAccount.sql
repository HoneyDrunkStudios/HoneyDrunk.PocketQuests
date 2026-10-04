CREATE PROCEDURE [pocketquests].[InitializeAccount]
    @IdentityUserId varchar(30), @TimeZoneId varchar(100), @Now datetimeoffset(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    EXEC [pocketquests].[LockAccount] @IdentityUserId;
    IF NOT EXISTS (SELECT 1 FROM [pocketquests].[Account] WHERE IdentityUserId=@IdentityUserId)
        INSERT [pocketquests].[Account]
            (Id,IdentityUserId,TimeZoneId,LastRecordedAt,MutationVersion,ProjectionVersion,ProjectionAsOfAt,CreatedAt,ModifiedAt)
        VALUES (NEWID(),@IdentityUserId,@TimeZoneId,@Now,0,0,@Now,@Now,@Now);
    UPDATE b SET AccountId=a.Id
        FROM [pocketquests].[AccountLifecycleState] b JOIN [pocketquests].[Account] a ON a.IdentityUserId=b.IdentityUserId
        WHERE b.IdentityUserId=@IdentityUserId AND b.AccountId IS NULL;
END;
GO
