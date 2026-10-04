-- Disposable fixture only; the product DACPAC never creates a login/user.
CREATE USER [pq_command_probe] WITHOUT LOGIN;
ALTER ROLE [pocketquests_command_runtime] ADD MEMBER [pq_command_probe];
GO
EXECUTE AS USER = 'pq_command_probe';
BEGIN TRY
    BEGIN TRANSACTION;
    EXEC pocketquests.InitializeAccount @IdentityUserId='usr_00000000000000000000000009',@TimeZoneId='Etc/UTC',@Now='2026-01-01T00:00:00+00:00';
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    REVERT;
    THROW;
END CATCH;
DECLARE @denied int=0;
BEGIN TRY UPDATE pocketquests.Account SET MutationVersion=999 WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @denied+=1; END CATCH;
BEGIN TRY DELETE pocketquests.CommandReceipt WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @denied+=1; END CATCH;
BEGIN TRY UPDATE pocketquests.QuestDefinitionRevision SET Title=N'forged' WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @denied+=1; END CATCH;
BEGIN TRY DELETE pocketquests.QuestOccurrenceEvent WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @denied+=1; END CATCH;
BEGIN TRY UPDATE pocketquests.QuestCompletion SET UndoneAt=NULL,UndoQuestOccurrenceEventId=NULL WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @denied+=1; END CATCH;
BEGIN TRY UPDATE pocketquests.XpBalance SET EarnedXp=99999 WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @denied+=1; END CATCH;
BEGIN TRY UPDATE dbo.AuditRecords SET Actor=N'forged' WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @denied+=1; END CATCH;
REVERT;
IF @denied<>7 THROW 51190,'A runtime principal bypass was allowed.',1;
DROP USER [pq_command_probe];
GO
