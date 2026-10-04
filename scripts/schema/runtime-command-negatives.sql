-- All rows are synthetic in the disposable test fixture. Every probe is rolled back.
BEGIN TRANSACTION;
EXEC pocketquests.InitializeAccount @IdentityUserId='usr_00000000000000000000000008',@TimeZoneId='Etc/UTC',@Now='2026-01-01T00:00:00+00:00';
EXEC pocketquests.IssueSyncAnchor @IdentityUserId='usr_00000000000000000000000008',
    @Id='00000000-0000-0000-0000-000000000009',@DeviceId='00000000-0000-0000-0000-000000000010',
    @BootId='00000000-0000-0000-0000-000000000011',@DeviceAt='2026-01-01T00:00:00+00:00',@Now='2026-01-01T00:00:00+00:00';
COMMIT;
GO
CREATE PROCEDURE dbo.SchemaTestInvalidCommand @Scenario int
AS
BEGIN
    DECLARE @attributes pocketquests.AllocationInput, @skills pocketquests.AllocationInput,
        @ledger pocketquests.LedgerInput, @balances pocketquests.BalanceInput,
        @streaks pocketquests.StreakInput, @entitlements pocketquests.EntitlementInput;
    DECLARE @operation uniqueidentifier=NEWID(), @occurrence uniqueidentifier=NEWID(),
        @action varchar(40)='accept', @expected bigint=0,
        @outcome nvarchar(max)=N'{"ProjectionAt":"2026-01-01T00:00:00+00:00","CompletionOutcome":null}',
        @at datetimeoffset(7)='2026-01-01T00:00:00+00:00', @recorded datetimeoffset(7)='2026-01-01T00:00:00+00:00',
        @anchor uniqueidentifier=NULL, @boot uniqueidentifier=NULL, @ordinal bigint=NULL, @elapsed float(53)=NULL;
    IF @Scenario=1 SET @operation='00000000-0000-0000-0000-000000000000';
    IF @Scenario=2 SET @outcome=N'{"ProjectionAt":"2026-01-01T00:00:00+00:00","Occurrences":[]}';
    IF @Scenario=3 SET @expected=99;
    IF @Scenario=4 INSERT @attributes VALUES('a01',9999);
    IF @Scenario=5 INSERT @attributes VALUES('unknown-attribute',10000);
    IF @Scenario=6 SET @action='complete';
    IF @Scenario=7 SET @recorded=DATEADD(second,6,@at);
    IF @Scenario=8 SET @anchor=NEWID();
    IF @Scenario=9
    BEGIN
        SET @expected=NULL;
        SET @anchor='00000000-0000-0000-0000-000000000009';
        SET @boot='00000000-0000-0000-0000-000000000011';
        SET @ordinal=1;
        SET @elapsed=0;
    END;
    EXEC pocketquests.CommitQuestCommand
        @IdentityUserId='usr_00000000000000000000000008',@ExpectedVersion=@expected,
        @OperationId=@operation,@Action=@action,@Digest=0x01,@Outcome=@outcome,@OccurrenceId=@occurrence,
        @RecordedAt=@recorded,@ProjectionAt=@at,@AsOfDate='2026-01-01',@Now=@at,@AnchorId=@anchor,@BootId=@boot,@Ordinal=@ordinal,@Elapsed=@elapsed,
        @SystemQuestId='PQ-CAT-Q01',@Title=N'Synthetic',@Criterion=N'Synthetic',@CategoryId='c01',
        @RankCode='F',@EffortCode='Small',@BaseXp=10,@DisplayLabels=N'{}',
        @Attributes=@attributes,@Skills=@skills,@Ledger=@ledger,@Balances=@balances,@Streaks=@streaks,@Entitlements=@entitlements,
        @AuditId='synthetic-procedure-negative',@AuditCategory=0,@AuditOutcome=0,@AuditOperation=0,@AuditTenant='internal',@AuditChanges=N'[]',@AuditMetadata=N'{}';
END;
GO
CREATE PROCEDURE dbo.SchemaTestExpectRejected @Scenario int, @Expected int
AS
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;
        EXEC dbo.SchemaTestInvalidCommand @Scenario;
        ROLLBACK;
        THROW 51191, 'Controlled writer unexpectedly accepted invalid input.', 1;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK;
        IF ERROR_NUMBER()<>@Expected THROW;
    END CATCH;
    IF EXISTS(SELECT 1 FROM pocketquests.Account WHERE IdentityUserId='usr_00000000000000000000000008' AND MutationVersion<>0)
       OR EXISTS(SELECT 1 FROM pocketquests.CommandReceipt r JOIN pocketquests.Account a ON a.Id=r.AccountId WHERE a.IdentityUserId='usr_00000000000000000000000008')
       OR EXISTS(SELECT 1 FROM pocketquests.QuestDefinition d JOIN pocketquests.Account a ON a.Id=d.AccountId WHERE a.IdentityUserId='usr_00000000000000000000000008')
       OR EXISTS(SELECT 1 FROM pocketquests.SyncAnchor s JOIN pocketquests.Account a ON a.Id=s.AccountId WHERE a.IdentityUserId='usr_00000000000000000000000008' AND (s.LastOrdinal<>0 OR s.LastElapsedMilliseconds<>0))
        THROW 51192,'A rejected command leaked receipt/history/version writes.',1;
END;
GO
EXEC dbo.SchemaTestExpectRejected 1,51109;
EXEC dbo.SchemaTestExpectRejected 2,51110;
EXEC dbo.SchemaTestExpectRejected 3,51108;
EXEC dbo.SchemaTestExpectRejected 4,51113;
EXEC dbo.SchemaTestExpectRejected 5,547;
EXEC dbo.SchemaTestExpectRejected 6,51117;
EXEC dbo.SchemaTestExpectRejected 7,51105;
EXEC dbo.SchemaTestExpectRejected 8,51112;
EXEC dbo.SchemaTestExpectRejected 9,51108;
GO

DROP PROCEDURE dbo.SchemaTestExpectRejected;
DROP PROCEDURE dbo.SchemaTestInvalidCommand;
GO
