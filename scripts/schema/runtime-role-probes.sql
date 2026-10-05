-- Only a loginless user in the disposable fixture. Production DACPAC provisions roles only.
CREATE USER [pq_command_probe] WITHOUT LOGIN;
ALTER ROLE [pocketquests_command_runtime] ADD MEMBER [pq_command_probe];
GO
EXECUTE AS USER = 'pq_command_probe';
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @now datetimeoffset(7)='2026-01-01T00:00:00+00:00';
    INSERT pocketquests.Account(Id,IdentityUserId,TimeZoneId,LastRecordedAt,ProjectionAsOfAt,CreatedAt,ModifiedAt)
        VALUES(NEWID(),'usr_00000000000000000000000009','Etc/UTC',@now,@now,@now,@now);
    UPDATE pocketquests.Account SET HasExpiryWarnings=0 WHERE IdentityUserId='usr_00000000000000000000000009';
    ROLLBACK;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    REVERT;
    THROW;
END CATCH;
DECLARE @denied int=0;
BEGIN TRY UPDATE pocketquests.Category SET Name=N'forged' WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (229,230) BEGIN REVERT; THROW; END; SET @denied+=1; END CATCH;
BEGIN TRY UPDATE pocketquests.CommandReceipt SET OutcomeJson=N'{}' WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (229,230) BEGIN REVERT; THROW; END; SET @denied+=1; END CATCH;
BEGIN TRY DELETE pocketquests.QuestDefinitionRevision WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (229,230) BEGIN REVERT; THROW; END; SET @denied+=1; END CATCH;
BEGIN TRY UPDATE pocketquests.AccountLifecycleState SET StateCode='Active' WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (229,230) BEGIN REVERT; THROW; END; SET @denied+=1; END CATCH;
BEGIN TRY DELETE pocketquests.ErasureMarker WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (229,230) BEGIN REVERT; THROW; END; SET @denied+=1; END CATCH;
BEGIN TRY UPDATE dbo.AuditRecords SET Actor=N'forged' WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (229,230) BEGIN REVERT; THROW; END; SET @denied+=1; END CATCH;
REVERT;
IF @denied<>6 THROW 51190,'An unauthorized runtime table permission was allowed.',1;
DROP USER [pq_command_probe];
GO
