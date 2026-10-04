"""Generate account-scoped FK-safe erasure; fail closed if a new owned table is not representable."""
from pathlib import Path
import argparse
import json

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT / 'HoneyDrunk.PocketQuests/PocketQuests.Database/Procedures/pocketquests.PurgeAccount.sql'


def render():
    tables = json.loads((ROOT / 'docs/schema/schema-contract.json').read_text(encoding='utf-8'))['tables']
    special = {'Account', 'AccountAuditRecord', 'LifecycleMessage', 'AccountLifecycleState', 'ErasureMarker'}
    owned = {t['name']: t for t in tables if any(c['name'] == 'AccountId' for c in t['columns']) and t['name'] not in special}
    remaining = set(owned)
    order = []
    while remaining:
        referenced = {f['referenced_table'] for name in remaining for f in owned[name]['foreign_keys']
                      if f['referenced_schema'] == 'pocketquests' and f['referenced_table'] != name}
        leaves = sorted(remaining - referenced)
        if not leaves:
            raise ValueError('Unreviewed erasure dependency cycle: ' + ', '.join(sorted(remaining)))
        order.extend(leaves)
        remaining.difference_update(leaves)
    for table in tables:
        if table['name'] not in special and table['name'] not in owned and table['kind'] != 'reference':
            raise ValueError('No reviewed erasure owner for ' + table['name'])
    deletes = '\n'.join(f'    DELETE [pocketquests].[{name}] WHERE AccountId=@account;' for name in order)
    return """-- Generated from schema-contract ownership/FKs by generate_erasure.py. Never disable constraints to erase.
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
""" + deletes + """
    DELETE [pocketquests].[Account] WHERE Id=@account;
    -- Duplicate live delivery or restore must never reset the verified 35-day clock.
    IF NOT EXISTS(SELECT 1 FROM [pocketquests].[ErasureMarker] WHERE Id=@IdentityUserId)
        INSERT [pocketquests].[ErasureMarker](Id,CreatedAt) VALUES(@IdentityUserId,@MarkerCreatedAt);
END;
GO
"""


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    expected = render()
    if args.check:
        if not TARGET.exists() or TARGET.read_text(encoding='utf-8') != expected:
            raise SystemExit('Generated erasure ownership/FK order drift.')
    else:
        TARGET.write_text(expected, encoding='utf-8', newline='\n')
    print('Erasure ownership and FK-safe deletion order match the reviewed schema.')
