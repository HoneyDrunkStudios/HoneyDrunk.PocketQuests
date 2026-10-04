-- Independent negative writes against the real generalized procedure under the runtime principal.
IF USER_ID('pq_generalized_probe') IS NULL CREATE USER pq_generalized_probe WITHOUT LOGIN;
ALTER ROLE pocketquests_command_runtime ADD MEMBER pq_generalized_probe;
BEGIN TRAN;
EXEC pocketquests.InitializeAccount 'usr_00000000000000000000000888','Etc/UTC','2026-01-01T12:00:00+00:00';
EXEC pocketquests.IssueSyncAnchor 'usr_00000000000000000000000888','88880000-0000-0000-0000-000000000001','88880000-0000-0000-0000-000000000002','88880000-0000-0000-0000-000000000003','2026-01-01T12:00:00+00:00','2026-01-01T12:00:00+00:00';
COMMIT;
GO
CREATE PROCEDURE pocketquests.SchemaTestGeneralizedProbe @Case int,@ExpectedError int
AS
BEGIN
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @owner varchar(30)='usr_00000000000000000000000888',@accountId uniqueidentifier,@operation uniqueidentifier=NEWID();
DECLARE @now datetimeoffset(7)='2026-01-01T12:00:01+00:00',@recorded datetimeoffset(7)='2026-01-01T12:00:01+00:00',@projection datetimeoffset(7)='2026-01-01T12:00:01+00:00';
DECLARE @expected bigint=0,@internal bit=0,@action varchar(40)='finish-onboarding',@anchor uniqueidentifier=NULL;
DECLARE @boot uniqueidentifier=NULL,@ordinal bigint=NULL,@elapsed float(53)=NULL;
DECLARE @outcome nvarchar(max)=N'{"ProjectionAt":"2026-01-01T12:00:01+00:00","CompletionOutcome":null}';
DECLARE @digest binary(32)=CONVERT(binary(32),0x01),@audit varchar(32)=REPLACE(CONVERT(varchar(36),NEWID()),'-','');
SELECT @accountId=Id FROM pocketquests.Account WHERE IdentityUserId=@owner;
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
UPDATE @Account SET IsOnboardingComplete=1,MutationVersion=1,ProjectionVersion=1,LastRecordedAt=@projection,ProjectionAsOfAt=@projection,ModifiedAt=@now;
INSERT @QuestCommandHistory(Id,AccountId,CommandReceiptId,AccountMutationVersion,ActionCode,RulesetVersion,TimeZoneBefore,ReconciledAt,RecordedAt,ProjectionAt,
    ConfirmPenalty,HasAcceptedTerms,ConfirmZoneChange,CreatedAt,ReconciliationLimit,ActionReconciliationLimit)
VALUES(@operation,@accountId,@operation,1,@action,'1.0','Etc/UTC',@now,@now,@now,0,0,0,@now,100,100);
IF @Case=1 SET @expected=NULL;
IF @Case=2 SET @now=NULL;
IF @Case=3 SET @recorded=NULL;
IF @Case=4 SET @projection=NULL;
IF @Case=5 SET @internal=NULL;
IF @Case=6 SET @action=NULL;
IF @Case=7 BEGIN SET @internal=1; SET @action='$reconcile'; SET @anchor='88880000-0000-0000-0000-000000000001'; SET @boot='88880000-0000-0000-0000-000000000003'; SET @ordinal=1; SET @elapsed=1000; END;
IF @Case=8 UPDATE @QuestCommandHistory SET CommandReceiptId=NULL;
IF @Case=9 UPDATE @QuestCommandHistory SET AccountId=NEWID();
IF @Case=10 UPDATE @Account SET CreatedAt=DATEADD(day,-1,CreatedAt);
IF @Case=11 BEGIN SET @internal=1; SET @action='$lifecycle-pause'; END;
IF @Case=12 SET @outcome=N'{"ProjectionAt":"2026-01-01T12:00:01+00:00","CompletionOutcome":null,"AccountSnapshot":{}}';
IF @Case=13 DELETE @QuestCommandHistory;
DECLARE @actual int=0;
BEGIN TRAN;
EXECUTE AS USER='pq_generalized_probe';
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
IF EXISTS(SELECT 1 FROM pocketquests.Account WHERE Id=@accountId AND (MutationVersion<>0 OR ProjectionVersion<>0 OR IsOnboardingComplete<>0))
   OR EXISTS(SELECT 1 FROM pocketquests.CommandReceipt WHERE Id=@operation)
   OR EXISTS(SELECT 1 FROM pocketquests.QuestCommandHistory WHERE Id=@operation)
   OR EXISTS(SELECT 1 FROM dbo.AuditRecords WHERE Id=@audit)
   OR EXISTS(SELECT 1 FROM pocketquests.SyncAnchor WHERE AccountId=@accountId AND (LastOrdinal<>0 OR LastElapsedMilliseconds<>0))
    THROW 51998,'A rejected mutation changed its transaction unit.',1;
END;
GO
EXEC pocketquests.SchemaTestGeneralizedProbe 1,51108;
EXEC pocketquests.SchemaTestGeneralizedProbe 2,51111;
EXEC pocketquests.SchemaTestGeneralizedProbe 3,51111;
EXEC pocketquests.SchemaTestGeneralizedProbe 4,51111;
EXEC pocketquests.SchemaTestGeneralizedProbe 5,51109;
EXEC pocketquests.SchemaTestGeneralizedProbe 6,51109;
EXEC pocketquests.SchemaTestGeneralizedProbe 7,51112;
EXEC pocketquests.SchemaTestGeneralizedProbe 8,51201;
EXEC pocketquests.SchemaTestGeneralizedProbe 9,51204;
EXEC pocketquests.SchemaTestGeneralizedProbe 10,51205;
EXEC pocketquests.SchemaTestGeneralizedProbe 11,51300;
EXEC pocketquests.SchemaTestGeneralizedProbe 12,51110;
EXEC pocketquests.SchemaTestGeneralizedProbe 13,51201;
-- The identical valid account/history inputs must reach a successful execution, rolled back by the probe.
EXEC pocketquests.SchemaTestGeneralizedProbe 0,0;
GO
DROP PROCEDURE pocketquests.SchemaTestGeneralizedProbe;
GO
