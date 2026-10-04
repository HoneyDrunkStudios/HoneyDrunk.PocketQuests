-- Generated grants: each table input is data, never direct table-write permission.
GRANT EXECUTE ON OBJECT::[pocketquests].[CommitAccountMutation] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[AccountMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[CustomSkillMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestDefinitionMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestDefinitionRevisionMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestDefinitionAttributeAllocationMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestDefinitionSkillAllocationMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestSeriesMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestSeriesRevisionMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestOccurrenceMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestOccurrenceRevisionMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestOccurrenceEventMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestCompletionMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[AccountPauseMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[TimeZoneChangeMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[SkillAssessmentMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestCommandHistoryMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestCommandInterestMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[AccountInterestMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[XpLedgerEntryMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[XpBalanceMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[CategoryProgressMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[AccountEntitlementMutationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[CommitAccountMutation] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[AccountMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[CustomSkillMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestDefinitionMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestDefinitionRevisionMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestDefinitionAttributeAllocationMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestDefinitionSkillAllocationMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestSeriesMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestSeriesRevisionMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestOccurrenceMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestOccurrenceRevisionMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestOccurrenceEventMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestCompletionMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[AccountPauseMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[TimeZoneChangeMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[SkillAssessmentMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestCommandHistoryMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[QuestCommandInterestMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[AccountInterestMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[XpLedgerEntryMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[XpBalanceMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[CategoryProgressMutationInput] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[AccountEntitlementMutationInput] TO [pocketquests_lifecycle_runtime];
GO
