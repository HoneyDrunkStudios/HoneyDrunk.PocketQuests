-- Internal application roles only. No users, logins or cloud principals are provisioned.
-- Ownership, lifecycle authorization and immutable-history guards execute in scoped Domain services.
-- SQL enforces relational/type/check/unique constraints; these roles are not end-user principals.
CREATE ROLE [pocketquests_command_runtime];
GO
CREATE ROLE [pocketquests_lifecycle_runtime];
GO
ALTER ROLE [pocketquests_command_runtime] ADD MEMBER [pocketquests_lifecycle_runtime];
GO
GRANT SELECT ON SCHEMA::[pocketquests] TO [pocketquests_command_runtime];
GO
-- First product initialization may attach an already verified Active lifecycle fence.
GRANT UPDATE ON OBJECT::[pocketquests].[AccountLifecycleState] ([AccountId]) TO [pocketquests_command_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[Account] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[Account] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[AccountAuditRecord] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[AccountAuditRecord] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE, DELETE ON OBJECT::[pocketquests].[AccountEntitlement] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[AccountEntitlement] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE, DELETE ON OBJECT::[pocketquests].[AccountInterest] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[AccountInterest] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE, DELETE ON OBJECT::[pocketquests].[AccountLifecycleState] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[AccountPause] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[AccountPause] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE, DELETE ON OBJECT::[pocketquests].[CategoryProgress] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[CategoryProgress] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[CommandReceipt] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[CommandReceipt] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[CustomSkill] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[CustomSkill] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE, DELETE ON OBJECT::[pocketquests].[ErasureMarker] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE, DELETE ON OBJECT::[pocketquests].[LifecycleMessage] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[QuestCommandHistory] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestCommandHistory] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[QuestCommandInterest] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestCommandInterest] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[QuestCompletion] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestCompletion] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[QuestDefinition] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestDefinition] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[QuestDefinitionAttributeAllocation] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestDefinitionAttributeAllocation] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[QuestDefinitionRevision] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestDefinitionRevision] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[QuestDefinitionSkillAllocation] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestDefinitionSkillAllocation] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[QuestOccurrence] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestOccurrence] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[QuestOccurrenceEvent] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestOccurrenceEvent] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[QuestOccurrenceRevision] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestOccurrenceRevision] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[QuestSeries] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestSeries] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT ON OBJECT::[pocketquests].[QuestSeriesRevision] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[QuestSeriesRevision] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[SkillAssessment] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[SkillAssessment] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[SyncAnchor] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[SyncAnchor] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE ON OBJECT::[pocketquests].[TimeZoneChange] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[TimeZoneChange] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE, DELETE ON OBJECT::[pocketquests].[XpBalance] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[XpBalance] TO [pocketquests_lifecycle_runtime];
GO
GRANT INSERT, UPDATE, DELETE ON OBJECT::[pocketquests].[XpLedgerEntry] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[pocketquests].[XpLedgerEntry] TO [pocketquests_lifecycle_runtime];
GO
GRANT SELECT, INSERT ON OBJECT::[dbo].[AuditRecords] TO [pocketquests_command_runtime];
GO
GRANT DELETE ON OBJECT::[dbo].[AuditRecords] TO [pocketquests_lifecycle_runtime];
GO
-- Shared Outbox dispatch UPDATE permission remains composed by its owner; never DENY it here.
GRANT SELECT, INSERT, DELETE ON OBJECT::[outbox].[OutboxMessages] TO [pocketquests_lifecycle_runtime];
GO
