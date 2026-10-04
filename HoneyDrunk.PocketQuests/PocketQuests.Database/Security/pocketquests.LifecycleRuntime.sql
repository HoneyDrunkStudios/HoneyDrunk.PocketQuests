-- No users or cloud principals are provisioned here. Host composition assigns this private service role.
CREATE ROLE [pocketquests_lifecycle_runtime];
GO
GRANT SELECT ON SCHEMA::[pocketquests] TO [pocketquests_lifecycle_runtime];
GO
DENY INSERT,UPDATE,DELETE ON SCHEMA::[pocketquests] TO [pocketquests_lifecycle_runtime];
GO
DENY INSERT,UPDATE,DELETE ON OBJECT::[dbo].[AuditRecords] TO [pocketquests_lifecycle_runtime];
GO
-- No direct Outbox grant is made here. Its owner composes the separate dispatcher permissions;
-- a product DENY would incorrectly override those shared-component grants on a composed principal.
GRANT EXECUTE ON OBJECT::[pocketquests].[AcquireAccountLock] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[SetLifecycleState] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[StageLifecycleAcknowledgment] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[PurgeAccount] TO [pocketquests_lifecycle_runtime];
GO
GRANT EXECUTE ON OBJECT::[pocketquests].[PruneLifecycle] TO [pocketquests_lifecycle_runtime];
GO
