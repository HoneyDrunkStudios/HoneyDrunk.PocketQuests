-- Shared component role is composed only for this disposable permission probe.
CREATE USER [pq_lifecycle_probe] WITHOUT LOGIN;
ALTER ROLE [pocketquests_lifecycle_runtime] ADD MEMBER [pq_lifecycle_probe];
CREATE ROLE [pq_shared_outbox_probe];
GRANT SELECT,UPDATE ON OBJECT::outbox.OutboxMessages TO [pq_shared_outbox_probe];
ALTER ROLE [pq_shared_outbox_probe] ADD MEMBER [pq_lifecycle_probe];
GO
EXECUTE AS USER='pq_lifecycle_probe';
BEGIN TRY
    BEGIN TRANSACTION;
    INSERT pocketquests.ErasureMarker(Id,CreatedAt) VALUES('usr_00000000000000000000000019','2026-01-01T00:00:00+00:00');
    DELETE pocketquests.ErasureMarker WHERE Id='usr_00000000000000000000000019';
    UPDATE outbox.OutboxMessages SET Status=Status WHERE 1=0;
    DELETE dbo.AuditRecords WHERE 1=0;
    DELETE pocketquests.QuestDefinitionRevision WHERE 1=0;
    ROLLBACK;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    REVERT;
    THROW;
END CATCH;
DECLARE @denied int=0;
BEGIN TRY UPDATE pocketquests.Category SET Name=N'forged' WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (229,230) BEGIN REVERT; THROW; END; SET @denied+=1; END CATCH;
BEGIN TRY UPDATE dbo.AuditRecords SET Actor=N'forged' WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (229,230) BEGIN REVERT; THROW; END; SET @denied+=1; END CATCH;
REVERT;
IF @denied<>2 THROW 51190,'Lifecycle role acquired unrelated catalog/Audit rewrite rights.',1;
DROP USER [pq_lifecycle_probe];
DROP ROLE [pq_shared_outbox_probe];
GO
