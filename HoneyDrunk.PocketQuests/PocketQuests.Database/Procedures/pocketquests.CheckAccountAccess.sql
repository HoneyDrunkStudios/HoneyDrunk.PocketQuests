-- Read-only lifecycle fence. Read transactions use ordinary repeatable SQL reads,
-- never the exclusive command application lock or implicit account initialization.
CREATE PROCEDURE [pocketquests].[CheckAccountAccess] @IdentityUserId varchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    IF @IdentityUserId IS NULL OR DATALENGTH(@IdentityUserId)<>30
       OR LEFT(@IdentityUserId,4) COLLATE Latin1_General_100_BIN2<>'usr_'
       OR SUBSTRING(@IdentityUserId,5,26) COLLATE Latin1_General_100_BIN2 LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%'
        THROW 51101, 'A verified canonical Identity user is required.', 1;
    IF EXISTS(SELECT 1 FROM [pocketquests].[ErasureMarker] WHERE Id=@IdentityUserId)
       OR EXISTS(SELECT 1 FROM [pocketquests].[AccountLifecycleState] WHERE IdentityUserId=@IdentityUserId AND StateCode<>'Active')
        THROW 51103, 'Account lifecycle does not allow product access.', 1;
END;
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[CheckAccountAccess] TO [pocketquests_command_runtime];
GO
