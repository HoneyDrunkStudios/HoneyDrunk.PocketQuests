-- Shared serialization resource for product commands and trusted Identity lifecycle transitions.
CREATE PROCEDURE [pocketquests].[AcquireAccountLock] @IdentityUserId varchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT=0 THROW 51100, 'An account transaction is required.', 1;
    IF @IdentityUserId IS NULL OR DATALENGTH(@IdentityUserId)<>30
       OR LEFT(@IdentityUserId,4) COLLATE Latin1_General_100_BIN2<>'usr_'
       OR SUBSTRING(@IdentityUserId,5,26) COLLATE Latin1_General_100_BIN2 LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%'
        THROW 51101, 'A verified canonical Identity user is required.', 1;
    DECLARE @result int,@resource nvarchar(255)=N'pocketquests:'+@IdentityUserId;
    EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @result<0 THROW 51102, 'Account lock unavailable.', 1;
END;
GO
