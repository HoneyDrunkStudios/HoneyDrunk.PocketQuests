-- Canonical Identity is resolved by the trusted application before calling this boundary.
-- This is a service role, not per-user SQL authentication. Never take IdentityUserId from a body.
CREATE PROCEDURE [pocketquests].[LockAccount]
    @IdentityUserId varchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    EXEC [pocketquests].[AcquireAccountLock] @IdentityUserId;
    IF EXISTS (SELECT 1 FROM [pocketquests].[ErasureMarker] WHERE Id=@IdentityUserId)
       OR EXISTS (SELECT 1 FROM [pocketquests].[AccountLifecycleState] WHERE IdentityUserId=@IdentityUserId AND StateCode <> 'Active')
        THROW 51103, 'Account lifecycle does not allow product access.', 1;
END;
GO
