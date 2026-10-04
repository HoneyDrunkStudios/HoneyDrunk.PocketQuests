-- table-design: mechanical schema checks for SQL Server / Azure SQL.
-- Read-only: queries the system catalog of the current database and changes nothing.
-- Fail   = mechanical rule, must be fixed.
-- Review = heuristic, fix it or say why not.
SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#t') IS NOT NULL DROP TABLE #t;
IF OBJECT_ID('tempdb..#r') IS NOT NULL DROP TABLE #r;

-- User tables, skipping temporal history tables and tool-owned tables such as __RefactorLog.
SELECT t.object_id, s.name AS SchemaName, t.name AS TableName,
       QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) AS FullName
INTO #t
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND t.temporal_type <> 1
  AND t.name NOT LIKE N'\_\_%' ESCAPE N'\';

CREATE TABLE #r (Severity varchar(6) NOT NULL, RuleId varchar(4) NOT NULL, ObjectName nvarchar(400) NOT NULL, Detail nvarchar(400) NOT NULL);

-- K01: table has no primary key.
INSERT #r SELECT 'Fail', 'K01', t.FullName, N'No primary key.'
FROM #t AS t
WHERE NOT EXISTS (SELECT 1 FROM sys.key_constraints AS k WHERE k.parent_object_id = t.object_id AND k.type = 'PK');

-- K02: foreign key has no index whose leading columns are the key columns.
INSERT #r SELECT 'Fail', 'K02', t.FullName, N'Foreign key ' + fk.name + N' has no supporting index.'
FROM sys.foreign_keys AS fk
JOIN #t AS t ON t.object_id = fk.parent_object_id
WHERE NOT EXISTS (
    SELECT 1 FROM sys.indexes AS i
    WHERE i.object_id = fk.parent_object_id AND i.type IN (1, 2) AND i.is_hypothetical = 0
      -- A filtered index counts only when its filter is IS NOT NULL.
      AND (i.has_filter = 0
           OR (i.filter_definition LIKE N'%IS NOT NULL%' AND i.filter_definition NOT LIKE N'%IS NULL%'
               AND i.filter_definition NOT LIKE N'%=%' AND i.filter_definition NOT LIKE N'%<%' AND i.filter_definition NOT LIKE N'%>%'))
      AND NOT EXISTS (
          SELECT 1 FROM sys.foreign_key_columns AS fkc
          WHERE fkc.constraint_object_id = fk.object_id
            AND NOT EXISTS (
                SELECT 1 FROM sys.index_columns AS ic
                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                  AND ic.column_id = fkc.parent_column_id
                  AND ic.key_ordinal BETWEEN 1 AND (SELECT COUNT(*) FROM sys.foreign_key_columns AS x WHERE x.constraint_object_id = fk.object_id))));

-- K03: a column named like a reference has no foreign key.
INSERT #r SELECT 'Review', 'K03', t.FullName + N'.' + QUOTENAME(c.name), N'Named like a reference but has no foreign key. Add one, or describe the external source.'
FROM #t AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
WHERE c.name COLLATE Latin1_General_CS_AS LIKE N'%Id' AND c.name <> N'Id'
  AND NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns AS fkc WHERE fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id);

-- D01: table has no description.
INSERT #r SELECT 'Fail', 'D01', t.FullName, N'Table has no MS_Description.'
FROM #t AS t
WHERE NOT EXISTS (SELECT 1 FROM sys.extended_properties AS ep
                  WHERE ep.class = 1 AND ep.major_id = t.object_id AND ep.minor_id = 0
                    AND ep.name = N'MS_Description' AND LEN(CAST(ep.value AS nvarchar(4000))) > 0);

-- D02: column has no description.
INSERT #r SELECT 'Fail', 'D02', t.FullName + N'.' + QUOTENAME(c.name), N'Column has no MS_Description.'
FROM #t AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
WHERE NOT EXISTS (SELECT 1 FROM sys.extended_properties AS ep
                  WHERE ep.class = 1 AND ep.major_id = c.object_id AND ep.minor_id = c.column_id
                    AND ep.name = N'MS_Description' AND LEN(CAST(ep.value AS nvarchar(4000))) > 0);

-- N01: constraint was named by the server.
INSERT #r SELECT 'Fail', 'N01', t.FullName, N'System-named constraint ' + x.name + N'. Name it explicitly.'
FROM #t AS t
JOIN (SELECT parent_object_id, name FROM sys.key_constraints WHERE is_system_named = 1
      UNION ALL SELECT parent_object_id, name FROM sys.default_constraints WHERE is_system_named = 1
      UNION ALL SELECT parent_object_id, name FROM sys.check_constraints WHERE is_system_named = 1
      UNION ALL SELECT parent_object_id, name FROM sys.foreign_keys WHERE is_system_named = 1) AS x
  ON x.parent_object_id = t.object_id;

-- N02: table name looks plural.
INSERT #r SELECT 'Review', 'N02', t.FullName, N'Table name looks plural. Table names are singular.'
FROM #t AS t
WHERE t.TableName LIKE N'%s' AND t.TableName NOT LIKE N'%ss' AND t.TableName NOT LIKE N'%us' AND t.TableName NOT LIKE N'%is';

-- N03: table is in dbo.
INSERT #r SELECT 'Review', 'N03', t.FullName, N'Table is in dbo. Use a schema named for the bounded context.'
FROM #t AS t WHERE t.SchemaName = N'dbo';

-- T01: unbounded text or binary column.
INSERT #r SELECT 'Review', 'T01', t.FullName + N'.' + QUOTENAME(c.name), N'Unbounded ' + ty.name + N'(max). Keep only for real documents; promote anything filtered, joined or constrained to columns.'
FROM #t AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
WHERE c.max_length = -1 AND ty.name IN (N'varchar', N'nvarchar', N'varbinary');

-- T02: a date or time stored as text.
INSERT #r SELECT 'Fail', 'T02', t.FullName + N'.' + QUOTENAME(c.name), N'Named like a date or time but stored as ' + ty.name + N'.'
FROM #t AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
WHERE ty.name IN (N'char', N'varchar', N'nchar', N'nvarchar')
  AND (c.name COLLATE Latin1_General_CS_AS LIKE N'%Date'
    OR c.name COLLATE Latin1_General_CS_AS LIKE N'%Time'
    OR c.name COLLATE Latin1_General_CS_AS LIKE N'%At'
    OR c.name COLLATE Latin1_General_CS_AS LIKE N'%On');

-- T03: approximate numeric column.
INSERT #r SELECT 'Review', 'T03', t.FullName + N'.' + QUOTENAME(c.name), N'Approximate type ' + ty.name + N'. Use an exact type for anything compared or summed.'
FROM #t AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
WHERE ty.name IN (N'float', N'real');

-- A01: table has no CreatedAt stamp.
INSERT #r SELECT 'Fail', 'A01', t.FullName, N'No CreatedAt column.'
FROM #t AS t
WHERE NOT EXISTS (SELECT 1 FROM sys.columns AS c WHERE c.object_id = t.object_id AND c.name = N'CreatedAt');

-- A02: table records a modification time but has no rowversion.
INSERT #r SELECT 'Review', 'A02', t.FullName, N'Has ModifiedAt but no rowversion column for optimistic concurrency.'
FROM #t AS t
WHERE EXISTS (SELECT 1 FROM sys.columns AS c WHERE c.object_id = t.object_id AND c.name = N'ModifiedAt')
  AND NOT EXISTS (SELECT 1 FROM sys.columns AS c JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
                  WHERE c.object_id = t.object_id AND ty.name = N'timestamp');

SELECT Severity, RuleId, ObjectName, Detail FROM #r ORDER BY Severity, RuleId, ObjectName;

SELECT (SELECT COUNT(*) FROM #t) AS TablesChecked,
       (SELECT COUNT(*) FROM #r WHERE Severity = 'Fail') AS Fails,
       (SELECT COUNT(*) FROM #r WHERE Severity = 'Review') AS Reviews;
