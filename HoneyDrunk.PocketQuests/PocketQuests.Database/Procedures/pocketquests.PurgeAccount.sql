-- Generated from schema-contract ownership/FKs by generate_erasure.py. Never disable constraints to erase.
CREATE PROCEDURE [pocketquests].[PurgeAccount]
    @IdentityUserId varchar(30),@MarkerCreatedAt datetimeoffset(7),@Now datetimeoffset(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    EXEC [pocketquests].[AcquireAccountLock] @IdentityUserId;
    IF @Now IS NULL OR @MarkerCreatedAt IS NULL OR @MarkerCreatedAt>@Now OR @MarkerCreatedAt<=DATEADD(day,-35,@Now)
        THROW 51305, 'A current externally verified original erasure instant is required.', 1;
    DECLARE @account uniqueidentifier;
    SELECT @account=Id FROM [pocketquests].[Account] WHERE IdentityUserId=@IdentityUserId;
    DECLARE @audit TABLE(Id varchar(32) PRIMARY KEY);
    DECLARE @outbox TABLE(Id uniqueidentifier PRIMARY KEY);
    DELETE [pocketquests].[AccountAuditRecord] OUTPUT deleted.AuditRecordId INTO @audit WHERE AccountId=@account;
    DELETE a FROM [dbo].[AuditRecords] a JOIN @audit d ON d.Id=a.Id;
    DELETE [pocketquests].[LifecycleMessage] OUTPUT deleted.OutboxMessageId INTO @outbox WHERE IdentityUserId=@IdentityUserId;
    DELETE o FROM [outbox].[OutboxMessages] o JOIN @outbox d ON d.Id=o.Id;
    DELETE [pocketquests].[AccountLifecycleState] WHERE IdentityUserId=@IdentityUserId;
    UPDATE [pocketquests].[QuestOccurrence] SET ParentQuestOccurrenceId=NULL WHERE AccountId=@account;
    DELETE [pocketquests].[AccountEntitlement] WHERE AccountId=@account;
    DELETE [pocketquests].[AccountInterest] WHERE AccountId=@account;
    DELETE [pocketquests].[AccountPause] WHERE AccountId=@account;
    DELETE [pocketquests].[CategoryProgress] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestCommandInterest] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestDefinitionAttributeAllocation] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestDefinitionSkillAllocation] WHERE AccountId=@account;
    DELETE [pocketquests].[SkillAssessment] WHERE AccountId=@account;
    DELETE [pocketquests].[TimeZoneChange] WHERE AccountId=@account;
    DELETE [pocketquests].[XpBalance] WHERE AccountId=@account;
    DELETE [pocketquests].[XpLedgerEntry] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestCommandHistory] WHERE AccountId=@account;
    DELETE [pocketquests].[CustomSkill] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestCompletion] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestOccurrenceEvent] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestOccurrenceRevision] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestOccurrence] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestSeriesRevision] WHERE AccountId=@account;
    DELETE [pocketquests].[SyncAnchor] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestDefinitionRevision] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestSeries] WHERE AccountId=@account;
    DELETE [pocketquests].[CommandReceipt] WHERE AccountId=@account;
    DELETE [pocketquests].[QuestDefinition] WHERE AccountId=@account;
    DELETE [pocketquests].[Account] WHERE Id=@account;
    -- Duplicate live delivery or restore must never reset the verified 35-day clock.
    IF NOT EXISTS(SELECT 1 FROM [pocketquests].[ErasureMarker] WHERE Id=@IdentityUserId)
        INSERT [pocketquests].[ErasureMarker](Id,CreatedAt) VALUES(@IdentityUserId,@MarkerCreatedAt);
END;
GO
