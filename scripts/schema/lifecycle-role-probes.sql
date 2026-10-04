-- Only the isolated GUID fixture. Synthetic principals/dispatcher grant are rolled back.
BEGIN TRANSACTION;
CREATE USER [pq_lifecycle_probe] WITHOUT LOGIN;
CREATE USER [pq_lifecycle_product_probe] WITHOUT LOGIN;
CREATE ROLE [pq_shared_dispatch_probe];
ALTER ROLE [pocketquests_lifecycle_runtime] ADD MEMBER [pq_lifecycle_probe];
ALTER ROLE [pocketquests_command_runtime] ADD MEMBER [pq_lifecycle_product_probe];
EXECUTE AS USER='pq_lifecycle_product_probe';
BEGIN TRY
    EXEC pocketquests.InitializeAccount @IdentityUserId='usr_00000000000000000000000019',@TimeZoneId='Etc/UTC',@Now='2026-01-01T00:00:00+00:00';
    DECLARE @blocked int=0;
    BEGIN TRY EXEC pocketquests.SetLifecycleState; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @blocked+=1; END CATCH;
    BEGIN TRY EXEC pocketquests.StageLifecycleAcknowledgment; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @blocked+=1; END CATCH;
    BEGIN TRY EXEC pocketquests.PurgeAccount; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @blocked+=1; END CATCH;
    BEGIN TRY EXEC pocketquests.PruneLifecycle; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @blocked+=1; END CATCH;
    IF @blocked<>4 THROW 51991,'Product role reached private lifecycle capability.',1;
    REVERT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    REVERT;
    THROW;
END CATCH;
EXECUTE AS USER='pq_lifecycle_probe';
BEGIN TRY
    DECLARE @direct int=0;
    BEGIN TRY UPDATE pocketquests.AccountLifecycleState SET Version=999 WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @direct+=1; END CATCH;
    BEGIN TRY DELETE pocketquests.ErasureMarker WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @direct+=1; END CATCH;
    BEGIN TRY UPDATE dbo.AuditRecords SET Actor=N'forged' WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @direct+=1; END CATCH;
    BEGIN TRY UPDATE outbox.OutboxMessages SET Status=1 WHERE 1=0; END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; SET @direct+=1; END CATCH;
    IF @direct<>4 THROW 51991,'Private lifecycle role obtained direct table write access.',1;
    EXEC pocketquests.SetLifecycleState @IdentityUserId='usr_00000000000000000000000019',@ExpectedVersion=0,@Version=1,@State='Inactive',
        @EffectiveAt='2026-01-01T00:01:00+00:00',@PausedAt='2026-01-01T00:01:00+00:00',@Now='2026-01-01T00:01:00+00:00';
    EXEC pocketquests.StageLifecycleAcknowledgment @IdentityUserId='usr_00000000000000000000000019',@Version=1,
        @Id='aaaaaaaa-1111-2222-3333-444444444419',@Type=N'Synthetic.Ack',@Payload=N'{}',@Headers=N'{}',
        @ExpiresAt='2026-01-01T01:01:00+00:00',@Now='2026-01-01T00:01:00+00:00';
    IF NOT EXISTS(SELECT 1 FROM pocketquests.LifecycleMessage WHERE Id='aaaaaaaa-1111-2222-3333-444444444419')
        THROW 51991,'Private acknowledgment did not commit ownership.',1;
    REVERT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    REVERT;
    THROW;
END CATCH;
-- The Outbox owner, not the product DACPAC, supplies the dispatcher's grant.
GRANT SELECT,UPDATE ON OBJECT::outbox.OutboxMessages TO [pq_shared_dispatch_probe];
ALTER ROLE [pq_shared_dispatch_probe] ADD MEMBER [pq_lifecycle_probe];
EXECUTE AS USER='pq_lifecycle_probe';
BEGIN TRY
    IF NOT EXISTS(SELECT 1 FROM outbox.OutboxMessages WHERE Id='aaaaaaaa-1111-2222-3333-444444444419')
        THROW 51991,'Private procedure did not insert the shared envelope.',1;
    UPDATE outbox.OutboxMessages SET Status=2 WHERE Id='aaaaaaaa-1111-2222-3333-444444444419';
    IF @@ROWCOUNT<>1 THROW 51991,'Product permissions overrode a composed shared-owner grant.',1;
    EXEC pocketquests.PruneLifecycle @Now='2026-01-01T00:02:00+00:00',@DispatchedStatus=2;
    IF EXISTS(SELECT 1 FROM outbox.OutboxMessages WHERE Id='aaaaaaaa-1111-2222-3333-444444444419')
        THROW 51991,'Retention did not remove the owned dispatched envelope.',1;
    EXEC pocketquests.PurgeAccount @IdentityUserId='usr_00000000000000000000000019',
        @MarkerCreatedAt='2026-01-31T00:00:00+00:00',@Now='2026-01-31T00:00:00+00:00';
    IF EXISTS(SELECT 1 FROM pocketquests.Account WHERE IdentityUserId='usr_00000000000000000000000019')
        THROW 51991,'Private purge failed to remove the account.',1;
    IF NOT EXISTS(SELECT 1 FROM pocketquests.ErasureMarker WHERE Id='usr_00000000000000000000000019')
        THROW 51991,'Private purge failed to retain erasure evidence.',1;
    REVERT;
    ROLLBACK;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    REVERT;
    THROW;
END CATCH;
GO
