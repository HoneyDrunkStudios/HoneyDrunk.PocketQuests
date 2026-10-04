-- Fixture-owned data is seeded through the real command writer before these independent role probes.
CREATE USER pq_series_probe WITHOUT LOGIN;
ALTER ROLE pocketquests_command_runtime ADD MEMBER pq_series_probe;
GO
CREATE PROCEDURE pocketquests.SchemaTestSeriesProbe @Case int,@ExpectedError int
AS
BEGIN
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @owner varchar(30)='usr_00000000000000000000000999',@accountId uniqueidentifier,@operation uniqueidentifier=NEWID();
DECLARE @now datetimeoffset(7)='2026-01-01T12:00:01+00:00',@recorded datetimeoffset(7)='2026-01-01T12:00:01+00:00',@projection datetimeoffset(7)='2026-01-01T12:00:01+00:00';
DECLARE @expected bigint=0,@internal bit=0,@action varchar(40)='finish-onboarding',@anchor uniqueidentifier=NULL;
DECLARE @boot uniqueidentifier=NULL,@ordinal bigint=NULL,@elapsed float(53)=NULL;
DECLARE @outcome nvarchar(max)=N'{"ProjectionAt":"2026-01-01T12:00:01+00:00","CompletionOutcome":null}';
DECLARE @digest binary(32)=CONVERT(binary(32),0x01),@audit varchar(32)=REPLACE(CONVERT(varchar(36),NEWID()),'-','');
SELECT @accountId=Id,@expected=MutationVersion FROM pocketquests.Account WHERE IdentityUserId=@owner;
DECLARE @Account pocketquests.[AccountMutationInput];
DECLARE @CustomSkill pocketquests.[CustomSkillMutationInput];
DECLARE @QuestDefinition pocketquests.[QuestDefinitionMutationInput];
DECLARE @QuestDefinitionRevision pocketquests.[QuestDefinitionRevisionMutationInput];
DECLARE @QuestDefinitionAttributeAllocation pocketquests.[QuestDefinitionAttributeAllocationMutationInput];
DECLARE @QuestDefinitionSkillAllocation pocketquests.[QuestDefinitionSkillAllocationMutationInput];
DECLARE @QuestSeries pocketquests.[QuestSeriesMutationInput];
DECLARE @QuestSeriesRevision pocketquests.[QuestSeriesRevisionMutationInput];
DECLARE @QuestOccurrence pocketquests.[QuestOccurrenceMutationInput];
DECLARE @QuestOccurrenceRevision pocketquests.[QuestOccurrenceRevisionMutationInput];
DECLARE @QuestOccurrenceEvent pocketquests.[QuestOccurrenceEventMutationInput];
DECLARE @QuestCompletion pocketquests.[QuestCompletionMutationInput];
DECLARE @AccountPause pocketquests.[AccountPauseMutationInput];
DECLARE @TimeZoneChange pocketquests.[TimeZoneChangeMutationInput];
DECLARE @SkillAssessment pocketquests.[SkillAssessmentMutationInput];
DECLARE @QuestCommandHistory pocketquests.[QuestCommandHistoryMutationInput];
DECLARE @QuestCommandInterest pocketquests.[QuestCommandInterestMutationInput];
DECLARE @AccountInterest pocketquests.[AccountInterestMutationInput];
DECLARE @XpLedgerEntry pocketquests.[XpLedgerEntryMutationInput];
DECLARE @XpBalance pocketquests.[XpBalanceMutationInput];
DECLARE @CategoryProgress pocketquests.[CategoryProgressMutationInput];
DECLARE @AccountEntitlement pocketquests.[AccountEntitlementMutationInput];
INSERT @Account([Id],[IdentityUserId],[TimeZoneId],[IsOnboardingComplete],[HasExpiryWarnings],[IsAccountPaused],[SelectedBadgeId],[SelectedBadgeKind],[SelectedFrameId],[SelectedFrameKind],[LastRecordedAt],[MutationVersion],[ProjectionVersion],[ProjectionAsOfAt],[CreatedAt],[ModifiedAt],[HasPendingReconciliation]) SELECT [Id],[IdentityUserId],[TimeZoneId],[IsOnboardingComplete],[HasExpiryWarnings],[IsAccountPaused],[SelectedBadgeId],[SelectedBadgeKind],[SelectedFrameId],[SelectedFrameKind],[LastRecordedAt],[MutationVersion],[ProjectionVersion],[ProjectionAsOfAt],[CreatedAt],[ModifiedAt],[HasPendingReconciliation] FROM pocketquests.Account WHERE Id=@accountId;
UPDATE @Account SET IsOnboardingComplete=1,MutationVersion=@expected+1,ProjectionVersion=@expected+1,LastRecordedAt=@projection,ProjectionAsOfAt=@projection,ModifiedAt=@now;
INSERT @QuestCommandHistory(Id,AccountId,CommandReceiptId,AccountMutationVersion,ActionCode,RulesetVersion,TimeZoneBefore,ReconciledAt,RecordedAt,ProjectionAt,
    ConfirmPenalty,HasAcceptedTerms,ConfirmZoneChange,CreatedAt,ReconciliationLimit,ActionReconciliationLimit)
VALUES(@operation,@accountId,@operation,@expected+1,@action,'1.0','Etc/UTC',@now,@now,@now,0,0,0,@now,100,100);
IF @Case>0
BEGIN
    INSERT @QuestSeries([Id],[AccountId],[QuestDefinitionId],[Revision],[NextSequence],[PauseDays],[NextDeliveryOn],[StoppedAt],[CreatedAt],[ModifiedAt],[CreationOrdinal]) SELECT s.[Id],s.[AccountId],s.[QuestDefinitionId],s.[Revision],s.[NextSequence],s.[PauseDays],s.[NextDeliveryOn],s.[StoppedAt],s.[CreatedAt],s.[ModifiedAt],s.[CreationOrdinal] FROM pocketquests.QuestSeries s
        JOIN pocketquests.QuestDefinition d ON d.AccountId=s.AccountId AND d.Id=s.QuestDefinitionId
        WHERE s.AccountId=@accountId AND d.SystemQuestId='PQ-CAT-Q01';
    UPDATE @QuestSeries SET QuestDefinitionId=(SELECT Id FROM pocketquests.QuestDefinition WHERE AccountId=@accountId AND SystemQuestId='PQ-CAT-Q02');
    IF @Case>=2 BEGIN SET @action='save-series'; UPDATE @QuestCommandHistory SET ActionCode=@action; END;
    IF @Case=3 UPDATE @QuestSeries SET Revision=Revision+1;
END;
DECLARE @actual int=0;
BEGIN TRAN;
EXECUTE AS USER='pq_series_probe';
BEGIN TRY
EXEC pocketquests.CommitAccountMutation
    @IdentityUserId=@owner,@ExpectedVersion=@expected,@IsInternal=@internal,@OperationId=@operation,@Action=@action,@Digest=@digest,@Outcome=@outcome,
    @RecordedAt=@recorded,@ProjectionAt=@projection,@Now=@now,@AnchorId=@anchor,@BootId=@boot,@Ordinal=@ordinal,@Elapsed=@elapsed,
    @Account=@Account,
    @CustomSkill=@CustomSkill,
    @QuestDefinition=@QuestDefinition,
    @QuestDefinitionRevision=@QuestDefinitionRevision,
    @QuestDefinitionAttributeAllocation=@QuestDefinitionAttributeAllocation,
    @QuestDefinitionSkillAllocation=@QuestDefinitionSkillAllocation,
    @QuestSeries=@QuestSeries,
    @QuestSeriesRevision=@QuestSeriesRevision,
    @QuestOccurrence=@QuestOccurrence,
    @QuestOccurrenceRevision=@QuestOccurrenceRevision,
    @QuestOccurrenceEvent=@QuestOccurrenceEvent,
    @QuestCompletion=@QuestCompletion,
    @AccountPause=@AccountPause,
    @TimeZoneChange=@TimeZoneChange,
    @SkillAssessment=@SkillAssessment,
    @QuestCommandHistory=@QuestCommandHistory,
    @QuestCommandInterest=@QuestCommandInterest,
    @AccountInterest=@AccountInterest,
    @XpLedgerEntry=@XpLedgerEntry,
    @XpBalance=@XpBalance,
    @CategoryProgress=@CategoryProgress,
    @AccountEntitlement=@AccountEntitlement,
    @AuditId=@audit,@AuditCategory=1,@AuditOutcome=0,@AuditOperation=2,@AuditTenant='internal',@AuditChanges=N'[]',@AuditMetadata=N'{}';
END TRY
BEGIN CATCH
    SET @actual=ERROR_NUMBER();
END CATCH;
IF XACT_STATE()<>0 ROLLBACK;
REVERT;
IF @actual<>@ExpectedError
BEGIN
    DECLARE @message nvarchar(2048)=CONCAT('Generalized probe ',@Case,' expected ',@ExpectedError,' but got ',@actual);
    THROW 51998,@message,1;
END;
IF EXISTS(SELECT 1 FROM pocketquests.Account WHERE Id=@accountId AND (MutationVersion<>@expected OR ProjectionVersion<>@expected OR IsOnboardingComplete<>0))
   OR EXISTS(SELECT 1 FROM pocketquests.CommandReceipt WHERE Id=@operation)
   OR EXISTS(SELECT 1 FROM pocketquests.QuestCommandHistory WHERE Id=@operation)
   OR EXISTS(SELECT 1 FROM dbo.AuditRecords WHERE Id=@audit)
   OR EXISTS(SELECT 1 FROM pocketquests.SyncAnchor WHERE AccountId=@accountId AND (LastOrdinal<>0 OR LastElapsedMilliseconds<>0))
    THROW 51998,'A rejected mutation changed its transaction unit.',1;
END;
GO
EXEC pocketquests.SchemaTestSeriesProbe 1,51206;
EXEC pocketquests.SchemaTestSeriesProbe 2,51206;
EXEC pocketquests.SchemaTestSeriesProbe 3,51206;
EXEC pocketquests.SchemaTestSeriesProbe 0,0;
GO
DROP PROCEDURE pocketquests.SchemaTestSeriesProbe;
DROP USER pq_series_probe;
GO
