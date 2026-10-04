-- Internal application service permissions. No users/logins are provisioned by the DACPAC.
CREATE ROLE [pocketquests_command_runtime];
GO
GRANT SELECT ON SCHEMA::[pocketquests] TO [pocketquests_command_runtime];
GO
DENY INSERT, UPDATE, DELETE ON SCHEMA::[pocketquests] TO [pocketquests_command_runtime];
GO
DENY INSERT, UPDATE, DELETE ON OBJECT::[dbo].[AuditRecords] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[LockAccount] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[InitializeAccount] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[IssueSyncAnchor] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[CommitQuestCommand] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[AllocationInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[LedgerInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[BalanceInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[StreakInput] TO [pocketquests_command_runtime];
GO
GRANT EXECUTE, REFERENCES ON TYPE::[pocketquests].[EntitlementInput] TO [pocketquests_command_runtime];
GO
