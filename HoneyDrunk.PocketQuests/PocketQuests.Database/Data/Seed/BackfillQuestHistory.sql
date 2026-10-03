-- Preserve the immutable reward snapshot when upgrading the original ledger.
UPDATE completion
SET QuestSnapshot = occurrence.QuestSnapshot
FROM dbo.Completions AS completion
INNER JOIN dbo.Occurrences AS occurrence
    ON occurrence.AccountId = completion.AccountId AND occurrence.Id = completion.OccurrenceId
WHERE completion.QuestSnapshot IS NULL;

-- Populate existing definitions once; subsequent publishes preserve history.
INSERT INTO dbo.DefinitionRevisions (AccountId, DefinitionId, Revision, Document)
SELECT definition.AccountId, definition.Id,
       CAST(JSON_VALUE(definition.Document, '$.Revision') AS int), definition.Document
FROM dbo.Definitions AS definition
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.DefinitionRevisions AS revision
    WHERE revision.AccountId = definition.AccountId
      AND revision.DefinitionId = definition.Id
      AND revision.Revision = CAST(JSON_VALUE(definition.Document, '$.Revision') AS int)
);
