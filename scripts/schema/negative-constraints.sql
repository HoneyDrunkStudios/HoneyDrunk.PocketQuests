-- Synthetic schema-only probes. All data is rolled back; no product behavior claim.
SET NOCOUNT ON;
SET XACT_ABORT OFF;
GO
CREATE PROCEDURE #ExpectError @Label nvarchar(100), @Statement nvarchar(max), @Error int, @Constraint nvarchar(128)
AS
BEGIN
    BEGIN TRY
        EXEC sys.sp_executesql @Statement;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER()=@Error AND CHARINDEX(@Constraint,ERROR_MESSAGE())>0
        BEGIN
            PRINT N'PASS: '+@Label;
            RETURN;
        END;
        THROW;
    END CATCH;
    THROW 51002,'A negative constraint probe unexpectedly succeeded.',1;
END;
GO
BEGIN TRANSACTION;
DECLARE @now datetimeoffset(7)=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00');
INSERT pocketquests.ProfileReward(Id,KindCode,Name,RequiredCount,RequiredRank,RulesetVersion) VALUES('B01','Badge',N'Test badge',1,'F','1');
INSERT pocketquests.Account(Id,IdentityUserId,TimeZoneId,LastRecordedAt,ProjectionAsOfAt)
VALUES('00000000-0000-0000-0000-000000000001','usr_'+REPLICATE('0',25)+'1','UTC',@now,@now),
      ('00000000-0000-0000-0000-000000000002','usr_'+REPLICATE('0',25)+'2','UTC',@now,@now);
INSERT pocketquests.CommandReceipt(Id,AccountId,CommandType,ApiVersion,PayloadDigest,DigestVersion,OutcomeVersion,OutcomeJson,AppliedMutationVersion)
VALUES('10000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','test',1,CONVERT(binary(32),0x01),1,1,N'{}',1);
INSERT pocketquests.QuestDefinition(Id,AccountId,Revision)
VALUES('20000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001',1);
INSERT pocketquests.QuestDefinitionRevision(Id,AccountId,QuestDefinitionId,Revision,Title,Criterion,CategoryId,RankCode,EffortCode,BaseXp,PenaltyPercent,RulesetVersion,DisplaySnapshotVersion,DisplaySnapshotJson,EffectiveAt)
VALUES('30000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001',1,N'Quest',N'Finish it','c01','F','Small',10,0,'1',1,N'{}',@now);
INSERT pocketquests.QuestDefinitionAttributeAllocation(AccountId,QuestDefinitionRevisionId,AttributeId,BasisPoints)
VALUES('00000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','a01',10000);
INSERT pocketquests.QuestOccurrence(Id,AccountId,QuestDefinitionId,QuestDefinitionRevisionId,CategoryId,StateCode,AcceptedAt,Revision)
VALUES('40000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','c01','Active',@now,1);
INSERT pocketquests.QuestOccurrenceRevision(Id,AccountId,QuestOccurrenceId,Revision,QuestDefinitionId,QuestDefinitionRevisionId,CategoryId,StateCode,AcceptedAt,AccountMutationVersion,EffectiveAt)
VALUES('50000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001',1,'20000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','c01','Active',@now,1,@now);
INSERT pocketquests.QuestOccurrenceEvent(Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,EventCode,EffectiveAt,AccountMutationVersion)
VALUES('60000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001','Completed',@now,1),
      ('60000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001','Completed',@now,2),
      ('60000000-0000-0000-0000-000000000003','00000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001','Undone',DATEADD(minute,1,@now),3);
INSERT pocketquests.QuestCompletion(Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,RecordedAt)
VALUES('60000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001',@now);

EXEC #ExpectError N'Cross-account definition reference', N'UPDATE pocketquests.QuestOccurrence SET AccountId=''00000000-0000-0000-0000-000000000002'' WHERE Id=''40000000-0000-0000-0000-000000000001''',547,N'FK_';
EXEC #ExpectError N'Unknown category FK',N'UPDATE pocketquests.QuestDefinitionRevision SET CategoryId=''missing''',547,N'FK_QuestDefinitionRevision_Category';
EXEC #ExpectError N'Penalty choices',N'UPDATE pocketquests.QuestDefinitionRevision SET PenaltyPercent=15',547,N'CK_QuestDefinitionRevision_PenaltyPercent';
EXEC #ExpectError N'Invalid JSON receipt',N'UPDATE pocketquests.CommandReceipt SET OutcomeJson=N''not-json''',547,N'CK_CommandReceipt_OutcomeDocument';
EXEC #ExpectError N'Oversized JSON receipt',N'UPDATE pocketquests.CommandReceipt SET OutcomeJson=N''{"value":"''+REPLICATE(CONVERT(nvarchar(max),N''x''),33000)+N''"}''',547,N'CK_CommandReceipt_OutcomeDocument';
EXEC #ExpectError N'UTC storage',N'UPDATE pocketquests.Account SET LastRecordedAt=SWITCHOFFSET(LastRecordedAt,''+01:00'')',547,N'CK_Account_LastRecordedAtUtc';
EXEC #ExpectError N'Schedule null tuple',N'UPDATE pocketquests.QuestOccurrence SET PlannedTime=''09:00:00''',547,N'CK_QuestOccurrence_ScheduleNulls';
EXEC #ExpectError N'Penalty null tuple',N'UPDATE pocketquests.QuestOccurrence SET LockedLoss=10',547,N'CK_QuestOccurrence_LockedPenalty';
EXEC #ExpectError N'Self parent',N'UPDATE pocketquests.QuestOccurrence SET ParentQuestOccurrenceId=Id',547,N'CK_QuestOccurrence_NotOwnParent';
EXEC #ExpectError N'Allocation range',N'UPDATE pocketquests.QuestDefinitionAttributeAllocation SET BasisPoints=10001',547,N'CK_QuestDefinitionAttributeAllocation_BasisPoints';
EXEC #ExpectError N'Wrong reward kind',N'UPDATE pocketquests.Account SET SelectedFrameId=''B01''',547,N'FK_Account_ProfileReward_SelectedFrame';
EXEC #ExpectError N'Wrong lifecycle owner',N'INSERT pocketquests.AccountLifecycleState(Id,IdentityUserId,AccountId,Version,StateCode,EffectiveAt) VALUES(NEWID(),''usr_''+REPLICATE(''0'',25)+''2'',''00000000-0000-0000-0000-000000000001'',1,''Active'',TODATETIMEOFFSET(SYSUTCDATETIME(),''+00:00''))',547,N'FK_AccountLifecycleState_Account';
EXEC #ExpectError N'One live completion',N'INSERT pocketquests.QuestCompletion(Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,RecordedAt) SELECT ''60000000-0000-0000-0000-000000000002'',AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,RecordedAt FROM pocketquests.QuestCompletion',2601,N'UQ_QuestCompletion_OneLive';
EXEC #ExpectError N'Exact Undo cutoff',N'UPDATE pocketquests.QuestCompletion SET UndoneAt=DATEADD(hour,24,RecordedAt),UndoQuestOccurrenceEventId=''60000000-0000-0000-0000-000000000003''',547,N'CK_QuestCompletion_Undo';
EXEC #ExpectError N'Overall target cannot be category',N'INSERT pocketquests.XpBalance(Id,AccountId,TrackCode,CategoryId,EarnedXp,SeedXp,Level,ProjectionVersion) VALUES(NEWID(),''00000000-0000-0000-0000-000000000001'',''Overall'',''c01'',0,0,1,0)',547,N'CK_XpBalance_TrackTarget';

EXEC #ExpectError N'Progression starts at level one',N'INSERT pocketquests.XpBalance(Id,AccountId,TrackCode,EarnedXp,SeedXp,Level,ProjectionVersion) VALUES(NEWID(),''00000000-0000-0000-0000-000000000001'',''Overall'',0,0,0,0)',547,N'CK_XpBalance_Level';
EXEC #ExpectError N'Category bonus cannot exceed twenty percent',N'INSERT pocketquests.CategoryProgress(Id,AccountId,CategoryId,StreakDays,BonusRatePercent,HasQualifiedToday,IsExplicitlyPaused,AsOfDate,TimeZoneId,ProjectionVersion) VALUES(NEWID(),''00000000-0000-0000-0000-000000000001'',''c01'',22,21,0,0,''2026-10-04'',''UTC'',0)',547,N'CK_CategoryProgress_BonusRatePercent';
EXEC #ExpectError N'Canonical pre-onboarding lifecycle key',N'INSERT pocketquests.AccountLifecycleState(Id,IdentityUserId,Version,StateCode,EffectiveAt) VALUES(NEWID(),''provider-subject'',1,''Active'',TODATETIMEOFFSET(SYSUTCDATETIME(),''+00:00''))',547,N'CK_AccountLifecycleState_IdentityUserId';

-- Zero-share allocations are legal and do not imply an all-positive pool rule.
UPDATE pocketquests.QuestDefinitionAttributeAllocation SET BasisPoints=0;
UPDATE pocketquests.QuestDefinitionAttributeAllocation SET BasisPoints=10000;

UPDATE pocketquests.QuestCompletion SET UndoneAt=DATEADD(minute,1,RecordedAt),UndoQuestOccurrenceEventId='60000000-0000-0000-0000-000000000003';
INSERT pocketquests.QuestCompletion(Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,RecordedAt)
SELECT '60000000-0000-0000-0000-000000000002',AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,RecordedAt FROM pocketquests.QuestCompletion;
IF (SELECT COUNT(*) FROM pocketquests.QuestCompletion WHERE UndoneAt IS NULL)<>1 THROW 51003,'Valid recompletion did not leave one live completion.',1;
PRINT 'PASS: valid Undo permits one new live completion';

-- Marker storage tests: these do not claim an application restore/retry writer exists.
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'pocketquests.ErasureMarker'))<>2
    THROW 51004,'Erasure marker must persist exactly two columns.',1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'pocketquests.ErasureMarker') AND name=N'CreatedAt' AND default_object_id=0 AND is_nullable=0)
    THROW 51005,'Original marker timestamp must be explicit, required and have no reset-to-now default.',1;
DECLARE @originalMarkerAt datetimeoffset(7)=DATEADD(day,-10,@now);
INSERT pocketquests.ErasureMarker(Id,CreatedAt) VALUES('usr_'+REPLICATE('0',25)+'9',@originalMarkerAt);
IF NOT EXISTS (SELECT 1 FROM pocketquests.ErasureMarker WHERE CreatedAt=@originalMarkerAt)
    THROW 51006,'Explicit original marker timestamp was not preserved.',1;
PRINT 'PASS: two-field marker stores the explicit original timestamp without a default';

EXEC #ExpectError N'Marker timestamp required',N'INSERT pocketquests.ErasureMarker(Id) VALUES(''usr_''+REPLICATE(''0'',25)+''8'')',515,N'CreatedAt';
EXEC #ExpectError N'Marker UTC storage',N'UPDATE pocketquests.ErasureMarker SET CreatedAt=SWITCHOFFSET(CreatedAt,''+01:00'')',547,N'CK_ErasureMarker_CreatedAtUtc';
EXEC #ExpectError N'Duplicate marker cannot replace original row by insert',N'INSERT pocketquests.ErasureMarker(Id,CreatedAt) SELECT Id,TODATETIMEOFFSET(SYSUTCDATETIME(),''+00:00'') FROM pocketquests.ErasureMarker',2627,N'PK_ErasureMarker';
IF NOT EXISTS (SELECT 1 FROM pocketquests.ErasureMarker WHERE CreatedAt=@originalMarkerAt)
    THROW 51007,'Rejected duplicate insert changed the original marker timestamp.',1;
DECLARE @markerExpiry datetimeoffset(7)=DATEADD(day,35,@originalMarkerAt);
IF EXISTS (SELECT 1 FROM pocketquests.ErasureMarker WHERE CreatedAt<=DATEADD(day,-35,DATEADD(nanosecond,-100,@markerExpiry)))
    THROW 51008,'Marker became eligible before 35 elapsed days.',1;
IF NOT EXISTS (SELECT 1 FROM pocketquests.ErasureMarker WHERE CreatedAt<=DATEADD(day,-35,@markerExpiry))
    THROW 51009,'Marker is not eligible at the original 35-day cutoff.',1;
PRINT 'PASS: retention predicate uses the original instant immediately before and at 35 elapsed days';
ROLLBACK;
PRINT 'All 21 declarative negative probes, valid zero-share/recompletion and marker storage/cutoff probes passed; synthetic data rolled back.';
