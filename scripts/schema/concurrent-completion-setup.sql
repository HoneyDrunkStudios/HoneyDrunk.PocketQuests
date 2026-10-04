-- Synthetic rows for the independent concurrent-writer test database only.
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
COMMIT;
