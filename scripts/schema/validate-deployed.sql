-- Read-only product-schema supplement to the unchanged table-design schema-checks.sql.
-- Run with sqlcmd -I -b, only against the isolated schema-under-test.
SET NOCOUNT ON;
DECLARE @schema sysname = N'pocketquests';
DECLARE @issues TABLE (Severity varchar(6), RuleId varchar(24), ObjectName nvarchar(400), Detail nvarchar(2000));

INSERT @issues
SELECT 'Fail','DB01',QUOTENAME(s.name)+N'.'+QUOTENAME(t.name)+N'.'+QUOTENAME(f.name),N'FK is disabled or untrusted.'
FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE s.name=@schema AND (f.is_disabled=1 OR f.is_not_trusted=1);

INSERT @issues
SELECT 'Fail','DB02',QUOTENAME(s.name)+N'.'+QUOTENAME(t.name)+N'.'+QUOTENAME(c.name),N'CHECK is disabled or untrusted.'
FROM sys.check_constraints c JOIN sys.tables t ON t.object_id=c.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE s.name=@schema AND (c.is_disabled=1 OR c.is_not_trusted=1);

INSERT @issues
SELECT 'Fail','DB03',QUOTENAME(s.name)+N'.'+QUOTENAME(t.name)+N'.'+QUOTENAME(i.name),N'Disabled or hypothetical index cannot satisfy a contract.'
FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE s.name=@schema AND i.index_id>0 AND (i.is_disabled=1 OR i.is_hypothetical=1);

-- Conservative exact-order coverage. Filtered indexes are not counted here; the draft
-- supplies unfiltered support where the partial index cannot cover every FK row.
INSERT @issues
SELECT 'Fail','DB04',QUOTENAME(s.name)+N'.'+QUOTENAME(t.name)+N'.'+QUOTENAME(f.name),N'No enabled unfiltered index has this FK as its exact ordered leading key.'
FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE s.name=@schema AND NOT EXISTS (
 SELECT 1 FROM sys.indexes i WHERE i.object_id=t.object_id AND i.type IN (1,2)
 AND i.is_disabled=0 AND i.is_hypothetical=0 AND i.has_filter=0
 AND NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id=f.object_id
   AND NOT EXISTS (SELECT 1 FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id
     AND ic.column_id=fc.parent_column_id AND ic.key_ordinal=fc.constraint_column_id)));

INSERT @issues
SELECT 'Fail','DB05',QUOTENAME(s.name)+N'.'+QUOTENAME(t.name)+N'.'+QUOTENAME(i.name),N'Additional index has no nonempty MS_Description explaining its purpose.'
FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE s.name=@schema AND i.index_id>0 AND i.is_primary_key=0 AND i.is_unique_constraint=0
AND NOT EXISTS (SELECT 1 FROM sys.extended_properties ep WHERE ep.class=7 AND ep.major_id=t.object_id AND ep.minor_id=i.index_id
 AND ep.name=N'MS_Description' AND LEN(CONVERT(nvarchar(4000),ep.value))>0);

SELECT Severity,RuleId,ObjectName,Detail FROM @issues ORDER BY RuleId,ObjectName;
IF EXISTS (SELECT 1 FROM @issues WHERE Severity='Fail') THROW 51001,'Product schema supplement failed.',1;
PRINT 'Product schema supplement passed.';
