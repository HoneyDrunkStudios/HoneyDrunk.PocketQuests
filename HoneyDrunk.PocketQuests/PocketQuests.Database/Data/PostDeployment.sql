-- Runs after every publish. Include only repeatable, reviewed seed scripts here.
-- Example: :r .\Seed\ReferenceData.sql
-- Repeatable upgrades for databases created by the original EF schema.
:r .\Seed\BackfillQuestHistory.sql

-- Additive relational reference data; no account migration or cutover.
:r .\Seed\PocketQuestsCatalog.sql
