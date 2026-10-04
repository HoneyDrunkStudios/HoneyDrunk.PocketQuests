-- One atomic command boundary. Domain supplies calibrated terms/projections; SQL owns
-- ownership, concurrency, append-only history, write-once Undo, allocation and receipt invariants.
CREATE PROCEDURE [pocketquests].[CommitQuestCommand]
    @IdentityUserId varchar(30), @ExpectedVersion bigint,
    @OperationId uniqueidentifier, @Action varchar(40), @Digest binary(32), @Outcome nvarchar(max),
    @OccurrenceId uniqueidentifier, @CompletionId uniqueidentifier = NULL,
    @RecordedAt datetimeoffset(7), @ProjectionAt datetimeoffset(7), @AsOfDate date, @Now datetimeoffset(7),
    @AnchorId uniqueidentifier = NULL, @BootId uniqueidentifier = NULL,
    @Ordinal bigint = NULL, @Elapsed float(53) = NULL,
    @SystemQuestId varchar(40) = NULL, @Title nvarchar(120) = NULL, @Criterion nvarchar(2000) = NULL,
    @Description nvarchar(2000) = NULL, @CategoryId varchar(40) = NULL,
    @RankCode varchar(1) = NULL, @EffortCode varchar(6) = NULL, @BaseXp bigint = NULL,
    @DisplayLabels nvarchar(max) = NULL,
    @DueOn date = NULL, @PlannedTime time(0) = NULL, @DeadlineAt datetimeoffset(7) = NULL,
    @Attributes [pocketquests].[AllocationInput] READONLY,
    @Skills [pocketquests].[AllocationInput] READONLY,
    @Ledger [pocketquests].[LedgerInput] READONLY,
    @Balances [pocketquests].[BalanceInput] READONLY,
    @Streaks [pocketquests].[StreakInput] READONLY,
    @Entitlements [pocketquests].[EntitlementInput] READONLY,
    @AuditId varchar(32), @AuditCategory int, @AuditOutcome int, @AuditOperation int,
    @AuditTenant varchar(100), @AuditCorrelation nvarchar(max) = NULL, @AuditChanges nvarchar(max), @AuditMetadata nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    EXEC [pocketquests].[LockAccount] @IdentityUserId;
    DECLARE @account uniqueidentifier, @version bigint, @zone varchar(100), @last datetimeoffset(7);
    SELECT @account=Id,@version=MutationVersion,@zone=TimeZoneId,@last=LastRecordedAt
        FROM [pocketquests].[Account] WHERE IdentityUserId=@IdentityUserId;
    IF @account IS NULL THROW 51104, 'Account was not initialized.', 1;
    -- Receipt lookup precedes concurrency/proof checks, including after later successful commands.
    IF EXISTS (SELECT 1 FROM [pocketquests].[CommandReceipt] WHERE AccountId=@account AND Id=@OperationId)
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [pocketquests].[CommandReceipt]
            WHERE AccountId=@account AND Id=@OperationId AND PayloadDigest=@Digest AND DigestVersion=1 AND ApiVersion=1 AND CommandType=@Action)
            THROW 51107, 'Operation ID already belongs to a different payload.', 1;
        RETURN;
    END;
    IF @ExpectedVersion IS NULL OR @version <> @ExpectedVersion THROW 51108, 'Account version changed.', 1;
    IF @OperationId='00000000-0000-0000-0000-000000000000' OR @Action NOT IN ('accept','complete','undo')
        THROW 51109, 'Unsupported command.', 1;
    IF ISJSON(@Outcome)<>1 OR DATALENGTH(@Outcome)>65536
       OR EXISTS(SELECT 1 FROM OPENJSON(@Outcome) WHERE [key] NOT IN ('ProjectionAt','CompletionOutcome'))
       OR TRY_CONVERT(datetimeoffset(7),JSON_VALUE(@Outcome,'$.ProjectionAt')) IS NULL
       OR TRY_CONVERT(datetimeoffset(7),JSON_VALUE(@Outcome,'$.ProjectionAt'))<>@ProjectionAt
        THROW 51110, 'Receipt must be a compact v1 outcome.', 1;
    IF @RecordedAt > DATEADD(second,5,@Now) THROW 51105, 'Server clock is not ready.', 1;
    IF @ProjectionAt < @RecordedAt OR @ProjectionAt < @Now OR @ProjectionAt < @last
        THROW 51111, 'Projection time cannot omit committed history.', 1;
    IF @AnchorId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM [pocketquests].[SyncAnchor]
        WHERE Id=@AnchorId AND AccountId=@account AND BootId=@BootId AND InvalidatedAt IS NULL
          AND @Ordinal>LastOrdinal AND @Elapsed>=LastElapsedMilliseconds AND @Elapsed>=0
          AND @RecordedAt>=RecordedTimeFloorAt)
        THROW 51112, 'Proof is unavailable or already consumed.', 1;
    IF @AnchorId IS NULL AND (@Ordinal IS NOT NULL OR @Elapsed IS NOT NULL OR @BootId IS NOT NULL OR @RecordedAt<@last)
        THROW 51112, 'Unanchored command cannot precede committed history.', 1;
    IF EXISTS(SELECT 1 FROM @Attributes) AND (SELECT SUM(CONVERT(bigint,BasisPoints)) FROM @Attributes)<>10000
        THROW 51113, 'Attribute allocation must total 10000.', 1;
    IF EXISTS(SELECT 1 FROM @Skills) AND (SELECT SUM(CONVERT(bigint,BasisPoints)) FROM @Skills)<>10000
        THROW 51113, 'Skill allocation must total 10000.', 1;
    SET @version=@version+1;
    INSERT [pocketquests].[CommandReceipt]
        (Id,AccountId,CommandType,ApiVersion,PayloadDigest,DigestVersion,OutcomeVersion,OutcomeJson,AppliedMutationVersion,CreatedAt)
        VALUES (@OperationId,@account,@Action,1,@Digest,1,1,@Outcome,@version,@Now);

    DECLARE @definition uniqueidentifier, @definitionRevision uniqueidentifier, @revision uniqueidentifier, @changed bit=0;
    IF @Action='accept'
    BEGIN
        IF NOT EXISTS(SELECT 1 FROM [pocketquests].[SystemQuest] WHERE Id=@SystemQuestId)
            THROW 51114, 'Only system quest acceptance is enabled in this writer.', 1;
        IF EXISTS(SELECT 1 FROM [pocketquests].[QuestOccurrence] WHERE Id=@OccurrenceId)
            THROW 51115, 'Occurrence ID already exists.', 1;
        SELECT @definition=Id FROM [pocketquests].[QuestDefinition] WHERE AccountId=@account AND SystemQuestId=@SystemQuestId;
        IF @definition IS NULL
        BEGIN
            SET @definition=NEWID(); SET @definitionRevision=NEWID();
            INSERT [pocketquests].[QuestDefinition](Id,AccountId,SystemQuestId,Revision,CreatedAt,ModifiedAt)
                VALUES(@definition,@account,@SystemQuestId,1,@Now,@Now);
            INSERT [pocketquests].[QuestDefinitionRevision]
                (Id,AccountId,QuestDefinitionId,Revision,Title,Criterion,Description,CategoryId,RankCode,EffortCode,BaseXp,
                 PenaltyPercent,RulesetVersion,DisplaySnapshotVersion,DisplaySnapshotJson,EffectiveAt,CommandReceiptId,CreatedAt)
                VALUES(@definitionRevision,@account,@definition,1,@Title,@Criterion,@Description,@CategoryId,@RankCode,@EffortCode,@BaseXp,
                    0,'1.0',1,@DisplayLabels,@RecordedAt,@OperationId,@Now);
            INSERT [pocketquests].[QuestDefinitionAttributeAllocation](AccountId,QuestDefinitionRevisionId,AttributeId,BasisPoints,CreatedAt)
                SELECT @account,@definitionRevision,TargetId,BasisPoints,@Now FROM @Attributes;
            INSERT [pocketquests].[QuestDefinitionSkillAllocation](Id,AccountId,QuestDefinitionRevisionId,SystemSkillId,BasisPoints,CreatedAt)
                SELECT NEWID(),@account,@definitionRevision,TargetId,BasisPoints,@Now FROM @Skills;
        END
        ELSE
        BEGIN
            SELECT @definitionRevision=Id FROM [pocketquests].[QuestDefinitionRevision] WHERE AccountId=@account AND QuestDefinitionId=@definition AND Revision=1
                AND Title=@Title AND Criterion=@Criterion AND ISNULL(Description,N'')=ISNULL(@Description,N'')
                AND CategoryId=@CategoryId AND RankCode=@RankCode AND EffortCode=@EffortCode AND BaseXp=@BaseXp AND PenaltyPercent=0;
            IF @definitionRevision IS NULL
                THROW 51116, 'Catalog revision changed; explicit revision support is required.', 1;
            IF EXISTS(SELECT TargetId,BasisPoints FROM @Attributes EXCEPT SELECT AttributeId,BasisPoints FROM [pocketquests].[QuestDefinitionAttributeAllocation] WHERE AccountId=@account AND QuestDefinitionRevisionId=@definitionRevision)
               OR EXISTS(SELECT AttributeId,BasisPoints FROM [pocketquests].[QuestDefinitionAttributeAllocation] WHERE AccountId=@account AND QuestDefinitionRevisionId=@definitionRevision EXCEPT SELECT TargetId,BasisPoints FROM @Attributes)
               OR EXISTS(SELECT TargetId,BasisPoints FROM @Skills EXCEPT SELECT SystemSkillId,BasisPoints FROM [pocketquests].[QuestDefinitionSkillAllocation] WHERE AccountId=@account AND QuestDefinitionRevisionId=@definitionRevision)
               OR EXISTS(SELECT SystemSkillId,BasisPoints FROM [pocketquests].[QuestDefinitionSkillAllocation] WHERE AccountId=@account AND QuestDefinitionRevisionId=@definitionRevision EXCEPT SELECT TargetId,BasisPoints FROM @Skills)
                THROW 51116, 'Catalog allocations changed; explicit revision support is required.', 1;
        END;
        SET @revision=NEWID();
        INSERT [pocketquests].[QuestOccurrence]
            (Id,AccountId,QuestDefinitionId,QuestDefinitionRevisionId,CategoryId,DueOn,PlannedTime,DeadlineAt,DeadlineTimeZoneId,
             StateCode,AcceptedAt,IsIndividuallyFrozen,Revision,SourceSyncAnchorId,CreatedAt,ModifiedAt)
            VALUES(@OccurrenceId,@account,@definition,@definitionRevision,@CategoryId,@DueOn,@PlannedTime,@DeadlineAt,
                CASE WHEN @DueOn IS NOT NULL THEN @zone END,'Active',@RecordedAt,0,1,@AnchorId,@Now,@Now);
        INSERT [pocketquests].[QuestOccurrenceRevision]
            (Id,AccountId,QuestOccurrenceId,Revision,QuestDefinitionId,QuestDefinitionRevisionId,CategoryId,DueOn,PlannedTime,
             DeadlineAt,DeadlineTimeZoneId,StateCode,AcceptedAt,IsIndividuallyFrozen,AccountMutationVersion,EffectiveAt,CommandReceiptId,CreatedAt)
            VALUES(@revision,@account,@OccurrenceId,1,@definition,@definitionRevision,@CategoryId,@DueOn,@PlannedTime,
                @DeadlineAt,CASE WHEN @DueOn IS NOT NULL THEN @zone END,'Active',@RecordedAt,0,@version,@RecordedAt,@OperationId,@Now);
        INSERT [pocketquests].[QuestOccurrenceEvent]
            (Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,EventCode,EffectiveAt,AccountMutationVersion,CommandReceiptId,CreatedAt)
            VALUES(@OperationId,@account,@OccurrenceId,@revision,'Accepted',@RecordedAt,@version,@OperationId,@Now);
        SET @changed=1;
    END
    ELSE
    BEGIN
        SELECT @revision=r.Id FROM [pocketquests].[QuestOccurrence] o JOIN [pocketquests].[QuestOccurrenceRevision] r
            ON r.AccountId=o.AccountId AND r.QuestOccurrenceId=o.Id AND r.Revision=o.Revision
            WHERE o.AccountId=@account AND o.Id=@OccurrenceId;
        IF @revision IS NULL THROW 51117, 'Occurrence does not belong to this account.', 1;
        IF @Action='complete' AND NOT EXISTS(SELECT 1 FROM [pocketquests].[QuestCompletion] WHERE AccountId=@account AND QuestOccurrenceId=@OccurrenceId AND UndoneAt IS NULL)
        BEGIN
            IF EXISTS(SELECT 1 FROM [pocketquests].[QuestOccurrence] WHERE AccountId=@account AND Id=@OccurrenceId
                AND (AcceptedAt IS NULL OR FrozenAt IS NOT NULL OR AbandonedAt IS NOT NULL OR AcceptedAt>@RecordedAt OR DeadlineAt<=@RecordedAt))
                THROW 51118, 'Occurrence cannot be completed at the recorded instant.', 1;
            INSERT [pocketquests].[QuestOccurrenceEvent]
                (Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,EventCode,EffectiveAt,AccountMutationVersion,CommandReceiptId,CreatedAt)
                VALUES(@OperationId,@account,@OccurrenceId,@revision,'Completed',@RecordedAt,@version,@OperationId,@Now);
            INSERT [pocketquests].[QuestCompletion]
                (Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,RecordedAt,CreatedAt,ModifiedAt)
                VALUES(@OperationId,@account,@OccurrenceId,@revision,@RecordedAt,@Now,@Now);
            UPDATE [pocketquests].[QuestOccurrence] SET StateCode='Completed',ModifiedAt=CASE WHEN ModifiedAt>@Now THEN ModifiedAt ELSE @Now END WHERE AccountId=@account AND Id=@OccurrenceId;
            SET @changed=1;
        END;
        IF @Action='undo'
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM [pocketquests].[QuestCompletion] WHERE AccountId=@account AND Id=@CompletionId AND QuestOccurrenceId=@OccurrenceId)
                THROW 51119, 'Completion does not belong to this account and occurrence.', 1;
            IF EXISTS(SELECT 1 FROM [pocketquests].[QuestCompletion] WHERE AccountId=@account AND Id=@CompletionId AND UndoneAt IS NULL)
            BEGIN
                IF EXISTS(SELECT 1 FROM [pocketquests].[QuestCompletion] WHERE AccountId=@account AND Id=@CompletionId AND (@RecordedAt<RecordedAt OR @RecordedAt>=DATEADD(hour,24,RecordedAt)))
                    THROW 51120, 'Undo is available for 24 elapsed hours.', 1;
                INSERT [pocketquests].[QuestOccurrenceEvent]
                    (Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,EventCode,EffectiveAt,AccountMutationVersion,CommandReceiptId,CreatedAt)
                    VALUES(@OperationId,@account,@OccurrenceId,@revision,'Undone',@RecordedAt,@version,@OperationId,@Now);
                UPDATE [pocketquests].[QuestCompletion] SET UndoneAt=@RecordedAt,UndoQuestOccurrenceEventId=@OperationId,
                    ModifiedAt=CASE WHEN ModifiedAt>@Now THEN ModifiedAt ELSE @Now END WHERE AccountId=@account AND Id=@CompletionId;
                UPDATE [pocketquests].[QuestOccurrence] SET StateCode=CASE WHEN DeadlineAt<=@ProjectionAt THEN 'Missed' ELSE 'Active' END,
                    ModifiedAt=CASE WHEN ModifiedAt>@Now THEN ModifiedAt ELSE @Now END WHERE AccountId=@account AND Id=@OccurrenceId;
                SET @changed=1;
            END;
        END;
    END;

    -- Recalculable projections are the only replaceable rows. Source facts are never rewritten.
    DELETE FROM [pocketquests].[XpLedgerEntry] WHERE AccountId=@account;
    INSERT [pocketquests].[XpLedgerEntry]
        (Id,AccountId,QuestOccurrenceEventId,ContributionCode,TrackCode,CategoryId,AttributeId,SystemSkillId,
         EffectiveAt,Amount,ProjectionVersion,RulesetVersion,CreatedAt,ModifiedAt)
        SELECT Id,@account,EventId,ContributionCode,TrackCode,
            CASE WHEN TrackCode='Category' THEN TargetId END,CASE WHEN TrackCode='Attribute' THEN TargetId END,
            CASE WHEN TrackCode='Skill' THEN TargetId END,EffectiveAt,Amount,@version,'1.0',@Now,@Now FROM @Ledger;
    DELETE FROM [pocketquests].[XpBalance] WHERE AccountId=@account;
    INSERT [pocketquests].[XpBalance]
        (Id,AccountId,TrackCode,CategoryId,AttributeId,SystemSkillId,EarnedXp,SeedXp,Level,ProjectionVersion,CreatedAt,ModifiedAt)
        SELECT Id,@account,TrackCode,CASE WHEN TrackCode='Category' THEN TargetId END,CASE WHEN TrackCode='Attribute' THEN TargetId END,
            CASE WHEN TrackCode='Skill' THEN TargetId END,EarnedXp,0,Level,@version,@Now,@Now FROM @Balances;
    DELETE FROM [pocketquests].[CategoryProgress] WHERE AccountId=@account;
    INSERT [pocketquests].[CategoryProgress]
        (Id,AccountId,CategoryId,StreakDays,BonusRatePercent,HasQualifiedToday,IsExplicitlyPaused,AsOfDate,TimeZoneId,ProjectionVersion,CreatedAt,ModifiedAt)
        SELECT Id,@account,CategoryId,Days,Bonus,HasQualifiedToday,0,@AsOfDate,@zone,@version,@Now,@Now FROM @Streaks;
    DELETE FROM [pocketquests].[AccountEntitlement] WHERE AccountId=@account;
    INSERT [pocketquests].[AccountEntitlement]
        (Id,AccountId,ProfileRewardId,QualifyingCount,IsEarned,ProjectionVersion,RulesetVersion,CreatedAt,ModifiedAt)
        SELECT Id,@account,RewardId,QualifyingCount,IsEarned,@version,'1.0',@Now,@Now FROM @Entitlements;
    IF @AnchorId IS NOT NULL
        UPDATE [pocketquests].[SyncAnchor] SET LastOrdinal=@Ordinal,LastElapsedMilliseconds=@Elapsed,
            ModifiedAt=CASE WHEN ModifiedAt>@Now THEN ModifiedAt ELSE @Now END WHERE AccountId=@account AND Id=@AnchorId;
    UPDATE [pocketquests].[Account] SET MutationVersion=@version,ProjectionVersion=@version,ProjectionAsOfAt=@ProjectionAt,
        LastRecordedAt=CASE WHEN LastRecordedAt>@RecordedAt THEN LastRecordedAt ELSE @RecordedAt END,
        ModifiedAt=CASE WHEN ModifiedAt>@Now THEN ModifiedAt ELSE @Now END WHERE Id=@account;
    IF @changed=1
    BEGIN
        -- Shared Audit owns this unchanged envelope. Its canonical factory supplies enum/JSON values.
        INSERT [dbo].[AuditRecords](Id,OccurredAt,Actor,EventName,Category,Outcome,TargetType,TargetId,TenantId,CorrelationId,Operation,ChangesJson,MetadataJson)
            VALUES(@AuditId,@Now,@IdentityUserId,N'pocketquests.quest.'+@Action,@AuditCategory,@AuditOutcome,N'quest.account',
                LOWER(REPLACE(CONVERT(varchar(36),@account),'-',''))+N'/'+LOWER(REPLACE(CONVERT(varchar(36),@OccurrenceId),'-','')),
                @AuditTenant,@AuditCorrelation,@AuditOperation,@AuditChanges,@AuditMetadata);
        INSERT [pocketquests].[AccountAuditRecord](AccountId,AuditRecordId,CreatedAt) VALUES(@account,@AuditId,@Now);
    END;
END;
GO
