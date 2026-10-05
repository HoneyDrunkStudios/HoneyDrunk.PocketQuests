using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.SqlServer.Dac;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Attributes;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Progress;
using System.Data;
using System.Text.Json;
using System.Xml.Linq;

namespace PocketQuests.SchemaTests;

/// <summary>Validates the staged schema against its contract, EF mapping and current domain rules.</summary>
/// <param name="fixture">An independently deployed, disposable SQL database.</param>
public sealed class RelationalSchemaTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private IEnumerable<JsonElement> Tables => fixture.Contract.RootElement.GetProperty("tables").EnumerateArray();

    /// <summary>Every staged column has matching SQL type, nullability, collation and useful metadata.</summary>
    /// <returns>Completion after catalog comparison.</returns>
    [Fact]
    public async Task DeployedColumnsAndMetadataMatchTheReviewedContract()
    {
        var rows = (await fixture.Query("""
            SELECT t.name AS TableName,c.name AS ColumnName,ty.name AS TypeName,c.max_length,c.precision,c.scale,
                   c.is_nullable,c.collation_name,c.is_identity,c.is_computed,
                   CONVERT(nvarchar(4000),te.value) AS TableDescription,CONVERT(nvarchar(4000),ce.value) AS ColumnDescription,
                   dc.name AS DefaultName
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
            LEFT JOIN sys.extended_properties te ON te.class=1 AND te.major_id=t.object_id AND te.minor_id=0 AND te.name='MS_Description'
            LEFT JOIN sys.extended_properties ce ON ce.class=1 AND ce.major_id=t.object_id AND ce.minor_id=c.column_id AND ce.name='MS_Description'
            LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
            WHERE s.name='pocketquests';
            SELECT CONVERT(nvarchar(128),DATABASEPROPERTYEX(DB_NAME(),'Collation')) AS CollationName;
            """)).ToArray();
        var columns = rows[0].Rows.Cast<DataRow>().ToDictionary(r => $"{r["TableName"]}.{r["ColumnName"]}");
        var defaultCollation = (string)rows[1].Rows[0][0];
        Assert.Equal(Tables.Sum(t => t.GetProperty("columns").GetArrayLength()), columns.Count);
        Assert.Equal(Tables.Select(t => Text(t, "name")).Order(), columns.Values.Select(r => (string)r["TableName"]).Distinct().Order());
        foreach (var table in Tables)
        {
            foreach (var column in table.GetProperty("columns").EnumerateArray())
            {
                var row = columns[$"{Text(table, "name")}.{Text(column, "name")}"];
                Assert.Equal(Text(table, "description"), row["TableDescription"]);
                Assert.Equal(Text(column, "description"), row["ColumnDescription"]);
                Assert.Equal(Text(column, "type"), SqlType(row));
                Assert.Equal(column.GetProperty("nullable").GetBoolean(), row["is_nullable"]);
                Assert.False((bool)row["is_identity"]);
                Assert.False((bool)row["is_computed"]);
                var collation = column.GetProperty("collation").GetString();
                Assert.Equal(collation == "DATABASE_DEFAULT" ? defaultCollation : collation, row["collation_name"] as string);
                Assert.Equal(column.GetProperty("default").ValueKind == JsonValueKind.Null ? null : $"DF_{Text(table, "name")}_{Text(column, "name")}", row["DefaultName"] as string);
            }
        }

        await fixture.Script("validate-deployed.sql");
    }

    /// <summary>Exact ordered keys, FK targets, delete actions and additional indexes survive DACPAC publication.</summary>
    /// <returns>Completion after relational comparison.</returns>
    [Fact]
    public async Task DeployedRelationshipsAndIndexesMatchTheContract()
    {
        var data = await fixture.Query("""
            SELECT t.name AS TableName,i.name AS IndexName,i.is_unique,i.is_primary_key,i.is_unique_constraint,i.type,
                   i.filter_definition,
                   STRING_AGG(CONVERT(nvarchar(max),c.name),',') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns,
                   CONVERT(nvarchar(4000),ep.value) AS Description
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.indexes i ON i.object_id=t.object_id
            JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0
            JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
            LEFT JOIN sys.extended_properties ep ON ep.class=7 AND ep.major_id=t.object_id AND ep.minor_id=i.index_id AND ep.name='MS_Description'
            WHERE s.name='pocketquests'
            GROUP BY t.name,i.name,i.is_unique,i.is_primary_key,i.is_unique_constraint,i.type,i.filter_definition,CONVERT(nvarchar(4000),ep.value);
            SELECT t.name AS TableName,f.name AS ForeignKeyName,rs.name AS ReferencedSchema,rt.name AS ReferencedTable,
                   f.delete_referential_action_desc,f.update_referential_action_desc,
                   STRING_AGG(CONVERT(nvarchar(max),c.name),',') WITHIN GROUP (ORDER BY fc.constraint_column_id) AS Columns,
                   STRING_AGG(CONVERT(nvarchar(max),rc.name),',') WITHIN GROUP (ORDER BY fc.constraint_column_id) AS ReferencedColumns
            FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.tables rt ON rt.object_id=f.referenced_object_id JOIN sys.schemas rs ON rs.schema_id=rt.schema_id
            JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
            JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=fc.parent_column_id
            JOIN sys.columns rc ON rc.object_id=rt.object_id AND rc.column_id=fc.referenced_column_id
            WHERE s.name='pocketquests' GROUP BY t.name,f.name,rs.name,rt.name,f.delete_referential_action_desc,f.update_referential_action_desc;
            SELECT ck.name FROM sys.check_constraints ck JOIN sys.tables t ON t.object_id=ck.parent_object_id
            JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='pocketquests';
            SELECT i.name FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id=ic.object_id AND i.index_id=ic.index_id
            JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
            WHERE s.name='pocketquests' AND ic.is_descending_key=1;
            SELECT i.name AS IndexName,c.name AS ColumnName FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id=ic.object_id AND i.index_id=ic.index_id
            JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
            WHERE s.name='pocketquests' AND ic.is_included_column=1;
            """);
        var indexes = data[0].Rows.Cast<DataRow>().ToDictionary(r => (string)r["IndexName"]);
        var foreignKeys = data[1].Rows.Cast<DataRow>().ToDictionary(r => (string)r["ForeignKeyName"]);
        Assert.Equal(Tables.Sum(t => 1 + t.GetProperty("unique_keys").GetArrayLength() + t.GetProperty("indexes").GetArrayLength()), indexes.Count);
        Assert.Equal(Tables.Sum(t => t.GetProperty("foreign_keys").GetArrayLength()), foreignKeys.Count);
        Assert.Equal(Tables.SelectMany(t => t.GetProperty("checks").EnumerateArray()).Select(c => Text(c, "name")).Order(), data[2].Rows.Cast<DataRow>().Select(r => (string)r[0]).Order());
        Assert.Empty(data[3].Rows.Cast<DataRow>());
        var expectedIncluded = Tables.SelectMany(t => t.GetProperty("indexes").EnumerateArray())
            .SelectMany(i => i.GetProperty("include").EnumerateArray().Select(c => Text(i, "name") + "/" + c.GetString())).Order();
        Assert.Equal(expectedIncluded, data[4].Rows.Cast<DataRow>().Select(r => (string)r[0] + "/" + (string)r[1]).Order());
        foreach (var table in Tables)
        {
            var primary = table.GetProperty("primary_key");
            foreach (var key in table.GetProperty("unique_keys").EnumerateArray().Prepend(primary))
            {
                var row = indexes[Text(key, "name")];
                Assert.Equal(Text(table, "name"), row["TableName"]);
                Assert.Equal(Join(key, "columns"), row["Columns"]);
                Assert.True((bool)row["is_unique"]);
                var isPrimary = Text(key, "name") == Text(primary, "name");
                Assert.Equal(isPrimary, row["is_primary_key"]);
                Assert.Equal(!isPrimary, row["is_unique_constraint"]);
                Assert.Equal((byte)(isPrimary ? 1 : 2), row["type"]);
                Assert.IsType<DBNull>(row["filter_definition"]);
            }

            foreach (var index in table.GetProperty("indexes").EnumerateArray())
            {
                var row = indexes[Text(index, "name")];
                Assert.Equal(Text(table, "name"), row["TableName"]);
                Assert.Equal(Join(index, "columns"), row["Columns"]);
                Assert.Equal(index.GetProperty("unique").GetBoolean(), row["is_unique"]);
                Assert.False((bool)row["is_primary_key"]);
                Assert.False((bool)row["is_unique_constraint"]);
                Assert.Equal((byte)2, row["type"]);
                Assert.Equal(Text(index, "purpose"), row["Description"]);

                // Full expression semantics (including filters/defaults/CHECKs) are independently
                // compared by DacFx's model comparator in the repeat-publication test.
                Assert.Equal(index.GetProperty("filter").ValueKind == JsonValueKind.Null, row["filter_definition"] is DBNull);
            }

            foreach (var fk in table.GetProperty("foreign_keys").EnumerateArray())
            {
                var row = foreignKeys[Text(fk, "name")];
                Assert.Equal(Text(table, "name"), row["TableName"]);
                Assert.Equal(Join(fk, "columns"), row["Columns"]);
                Assert.Equal(Join(fk, "referenced_columns"), row["ReferencedColumns"]);
                Assert.Equal(Text(fk, "referenced_table"), row["ReferencedTable"]);
                Assert.Equal(Text(fk, "referenced_schema"), row["ReferencedSchema"]);
                Assert.Equal("NO_ACTION", row["delete_referential_action_desc"]);
                Assert.Equal("NO_ACTION", row["update_referential_action_desc"]);
            }
        }
    }

    /// <summary>EF describes all product columns and account-owned relationships without a competing writer.</summary>
    [Fact]
    public void EfReadModelMatchesContractIncludingNullableUniqueTuplesAndRowVersions()
    {
        using var db = fixture.Context();
        var model = db.GetService<IDesignTimeModel>().Model;
        var entities = model.GetEntityTypes().Where(e => e.GetSchema() == "pocketquests").ToDictionary(e => e.GetTableName()!);
        Assert.Equal(Tables.Count(), entities.Count);
        foreach (var table in Tables)
        {
            var entity = entities[Text(table, "name")];
            Assert.Equal(Text(table, "description"), entity.GetComment());
            Assert.Equal(table.GetProperty("columns").GetArrayLength(), entity.GetProperties().Count());
            Assert.Equal(Join(table.GetProperty("primary_key"), "columns"), string.Join(',', entity.FindPrimaryKey()!.Properties.Select(p => p.Name)));
            foreach (var column in table.GetProperty("columns").EnumerateArray())
            {
                var property = entity.FindProperty(Text(column, "name"))!;
                Assert.Equal(Text(column, "type"), property.GetColumnType());
                Assert.Equal(column.GetProperty("nullable").GetBoolean(), property.IsNullable);
                Assert.Equal(Text(column, "description"), property.GetComment());
                Assert.Equal(column.GetProperty("default").GetString(), property.GetDefaultValueSql());
                Assert.Equal(Text(column, "type") == "rowversion", property.IsConcurrencyToken);
                var expected = Text(column, "type") == "rowversion" ? ValueGenerated.OnAddOrUpdate : column.GetProperty("default").ValueKind == JsonValueKind.Null ? ValueGenerated.Never : ValueGenerated.OnAdd;
                Assert.Equal(expected, property.ValueGenerated);
            }

            var mappedIndexes = entity.GetIndexes().ToDictionary(i => i.GetDatabaseName()!);
            foreach (var index in table.GetProperty("indexes").EnumerateArray())
            {
                var mapped = mappedIndexes[Text(index, "name")];
                Assert.Equal(Join(index, "columns"), string.Join(',', mapped.Properties.Select(p => p.Name)));
                Assert.Equal(Join(index, "include"), string.Join(',', mapped.GetIncludeProperties() ?? []));
                Assert.Equal(index.GetProperty("unique").GetBoolean(), mapped.IsUnique);
                Assert.Equal(index.GetProperty("filter").GetString(), mapped.GetFilter());
            }

            var fks = entity.GetForeignKeys().ToDictionary(f => f.GetConstraintName()!);
            Assert.Equal(table.GetProperty("foreign_keys").GetArrayLength(), fks.Count);
            foreach (var fk in table.GetProperty("foreign_keys").EnumerateArray())
            {
                var mapped = fks[Text(fk, "name")];
                Assert.Equal(Join(fk, "columns"), string.Join(',', mapped.Properties.Select(p => p.Name)));
                Assert.Equal(Join(fk, "referenced_columns"), string.Join(',', mapped.PrincipalKey.Properties.Select(p => p.Name)));
                Assert.Equal(Text(fk, "referenced_table"), mapped.PrincipalEntityType.GetTableName());
                Assert.Equal(Text(fk, "referenced_schema"), mapped.PrincipalEntityType.GetSchema());
                Assert.Equal(DeleteBehavior.NoAction, mapped.DeleteBehavior);
            }
        }

        Assert.Equal(QueryTrackingBehavior.NoTracking, db.ChangeTracker.QueryTrackingBehavior);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    /// <summary>Seeds preserve the domain catalog, rank rules and existing public IDs; repeat publish is a no-op.</summary>
    /// <returns>Completion after two-way read and semantic model checks.</returns>
    [Fact]
    public async Task CatalogRoundTripAndDacFxSemanticComparisonSurviveRepublish()
    {
        await using var db = fixture.Context();
        var categories = await db.Set<CategoryEntity>().OrderBy(r => r.SortOrder).ToListAsync();
        Assert.Equal(Catalog.Categories.Select(r => (id: r.Id, name: r.Name)), categories.Select(r => (id: r.Id, name: r.Name)));
        var attributes = await db.Set<AttributeEntity>().OrderBy(r => r.SortOrder).ToListAsync();
        Assert.Equal(Catalog.Attributes.Select(r => (id: r.Id, name: r.Name)), attributes.Select(r => (id: r.Id, name: r.Name)));
        var skills = await db.Set<SkillEntity>().OrderBy(r => r.SortOrder).ToListAsync();
        Assert.Equal(Catalog.Skills.Select(r => (id: r.Id, name: r.Name)), skills.Select(r => (id: r.Id, name: r.Name)));
        Assert.All(skills, r => Assert.Equal(r.Name.ToUpperInvariant(), r.NormalizedName));
        var quests = await db.Set<SystemQuestEntity>().OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(Catalog.Quests.Select(r => (id: r.Id, title: r.Title, categoryId: r.CategoryId)), quests.Select(r => (id: r.Id, title: r.Title, categoryId: r.CategoryId)));
        var rewards = await db.Set<ProfileRewardEntity>().OrderBy(r => r.Id).ToListAsync();
        var domain = Progression.Entitlements([], Rank.F);
        Assert.Equal(domain.Select(r => (id: r.Id, kind: r.Kind, name: r.Name, requiredCount: r.RequiredCount, r.RequiredRank.ToString())), rewards.Select(r => (id: r.Id, kindCode: r.KindCode, name: r.Name, requiredCount: r.RequiredCount, requiredRank: r.RequiredRank)));
        Assert.Empty(db.ChangeTracker.Entries());

        using var package = DacPackage.Load(Path.Combine(AppContext.BaseDirectory, "PocketQuests.Database.dacpac"));
        var service = fixture.Services();
        var report = service.GenerateDeployReport(package, fixture.Database, SchemaFixture.Options);
        await AssertSemanticParity(report);
        Evidence("dacfx-repeat-publish.xml", report);
        service.Deploy(package, fixture.Database, upgradeExisting: true, options: SchemaFixture.Options);
        var after = await db.Set<CategoryEntity>().OrderBy(r => r.SortOrder).ToListAsync();
        Assert.Equal(categories.Select(r => (id: r.Id, createdAt: r.CreatedAt, modifiedAt: r.ModifiedAt)), after.Select(r => (id: r.Id, createdAt: r.CreatedAt, modifiedAt: r.ModifiedAt)));
        await AssertSemanticParity(service.GenerateDeployReport(package, fixture.Database, SchemaFixture.Options));
    }

    /// <summary>The comparison gate actually detects semantic CHECK changes, then safely restores the scratch schema.</summary>
    /// <returns>Completion after the mutation probe.</returns>
    [Fact]
    public async Task DacFxComparatorRejectsAChangedConstraint()
    {
        using var package = DacPackage.Load(Path.Combine(AppContext.BaseDirectory, "PocketQuests.Database.dacpac"));
        try
        {
            await fixture.Execute("ALTER TABLE pocketquests.CategoryProgress DROP CONSTRAINT CK_CategoryProgress_BonusRatePercent; ALTER TABLE pocketquests.CategoryProgress WITH CHECK ADD CONSTRAINT CK_CategoryProgress_BonusRatePercent CHECK (BonusRatePercent BETWEEN 0 AND 21);");
            var report = fixture.Services().GenerateDeployReport(package, fixture.Database, SchemaFixture.Options);
            Assert.NotEmpty(Operations(report));
            Assert.Contains("CK_CategoryProgress_BonusRatePercent", report, StringComparison.Ordinal);
            Assert.Contains("CK_CategoryProgress_BonusRatePercent", await ExpressionDifferences());
            Evidence("dacfx-mutation-detected.xml", report);
        }
        finally
        {
            fixture.Services().Deploy(package, fixture.Database, upgradeExisting: true, options: SchemaFixture.Options);
        }

        await AssertSemanticParity(fixture.Services().GenerateDeployReport(package, fixture.Database, SchemaFixture.Options));
    }

    /// <summary>Actual SQL rejects malformed ownership, receipts, times and duplicate live completion while allowing Undo/recompletion.</summary>
    /// <returns>Completion after all rollback-only probes.</returns>
    [Fact]
    public async Task DeclarativeNegativeWritesAndMarkerRetentionPass()
    {
        await fixture.Script("negative-constraints.sql");
        await using var db = fixture.Context();
        Assert.Empty(await db.Set<AccountEntity>().ToListAsync());
        Assert.Empty(await db.Set<ErasureMarkerEntity>().ToListAsync());
        var exception = await Assert.ThrowsAsync<SqlException>(() => fixture.Execute("INSERT pocketquests.ErasureMarker(Id,CreatedAt) VALUES ('provider-subject',TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00'));"));
        Assert.Equal(547, exception.Number);
        Assert.Contains("CK_ErasureMarker_IdentityUserId", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Product failures fail the gate; each review is exact and shared-owner debt stays visible and unexpected objects fail.</summary>
    /// <returns>Completion after baseline findings are classified.</returns>
    [Fact]
    public async Task BaselineCheckerHasNoNewProductFailuresAndNoUnusedReviewDispositions()
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", "schema-checks.sql"));
        var results = await fixture.Query(script);
        var findings = results[0].Rows.Cast<DataRow>().Select(r => new Finding((string)r[0], (string)r[1], ((string)r[2]).Replace("[", string.Empty).Replace("]", string.Empty), (string)r[3])).ToArray();
        var product = findings.Where(f => f.Object.StartsWith("pocketquests.", StringComparison.Ordinal)).ToArray();
        var shared = findings.Where(f => f.Object.StartsWith("dbo.AuditRecords", StringComparison.Ordinal) || f.Object.StartsWith("outbox.OutboxMessages", StringComparison.Ordinal)).ToArray();
        var unexpected = findings.Except(product).Except(shared).ToArray();
        Assert.Empty(unexpected);
        Evidence("schema-checker-findings.json", JsonSerializer.Serialize(new { product, shared, unexpected }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.DoesNotContain(product, f => f.Severity == "Fail");
        var exceptions = fixture.Contract.RootElement.GetProperty("checker_exceptions").EnumerateArray().Select(e => (Text(e, "rule"), Text(e, "object"))).Order().ToArray();
        Assert.Equal(exceptions, product.Select(f => (rule: f.Rule, objectName: f.Object)).Order().ToArray());
        Assert.Equal(findings.Length, (int)results[1].Rows[0]["Fails"] + (int)results[1].Rows[0]["Reviews"]);
    }

    private static string SqlType(DataRow row)
    {
        var name = (string)row["TypeName"];
        return name switch
        {
            "timestamp" => "rowversion",
            "float" => "float(53)",
            "varchar" or "nvarchar" or "char" or "binary" => name + "(" + ((short)row["max_length"] == -1 ? "max" : ((short)row["max_length"] / (name == "nvarchar" ? 2 : 1)).ToString(System.Globalization.CultureInfo.InvariantCulture)) + ")",
            "time" or "datetimeoffset" => name + "(" + row["scale"] + ")",
            _ => name,
        };
    }

    private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString()!;

    private static string Join(JsonElement element, string name) => string.Join(',', element.GetProperty(name).EnumerateArray().Select(c => c.GetString()));

    private static IEnumerable<XElement> Operations(string report) => XDocument.Parse(report).Descendants().Where(e => e.Name.LocalName == "Operation");

    private static void Evidence(string name, string content)
    {
        var directory = Environment.GetEnvironmentVariable("POCKETQUESTS_SCHEMA_EVIDENCE");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), content);
        }
    }

    private async Task AssertSemanticParity(string report)
    {
        Evidence("dacfx-last-parity-report.xml", report);
        Assert.Empty(await ExpressionDifferences());
        var checks = Tables.SelectMany(t => t.GetProperty("checks").EnumerateArray()).Select(c => $"[pocketquests].[{Text(c, "name")}]").ToHashSet(StringComparer.Ordinal);
        foreach (var operation in Operations(report))
        {
            Assert.Contains((string?)operation.Attribute("Name"), new[] { "Create", "Drop" });
            foreach (var item in operation.Elements())
            {
                // DacFx conservatively re-emits server-expanded IN/BETWEEN CHECKs. Every
                // predicate is compared above; all other model operations remain failures.
                Assert.Equal("SqlCheckConstraint", (string?)item.Attribute("Type"));
                Assert.Contains((string)item.Attribute("Value")!, checks);
            }
        }
    }

    private async Task<List<string>> ExpressionDifferences()
    {
        var rows = (await fixture.Query("""
            SELECT ck.name AS Name,ck.definition AS Expression FROM sys.check_constraints ck
            JOIN sys.tables t ON t.object_id=ck.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='pocketquests'
            UNION ALL SELECT dc.name,dc.definition FROM sys.default_constraints dc
            JOIN sys.tables t ON t.object_id=dc.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='pocketquests'
            UNION ALL SELECT i.name,i.filter_definition FROM sys.indexes i
            JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='pocketquests' AND i.has_filter=1;
            """))[0].Rows.Cast<DataRow>().ToDictionary(r => (string)r[0], r => (string)r[1]);
        var expected = new Dictionary<string, (string expression, bool predicate)>(StringComparer.Ordinal);
        foreach (var table in Tables)
        {
            foreach (var check in table.GetProperty("checks").EnumerateArray())
                expected.Add(Text(check, "name"), (Text(check, "expression"), true));
            foreach (var column in table.GetProperty("columns").EnumerateArray().Where(c => c.GetProperty("default").ValueKind != JsonValueKind.Null))
                expected.Add($"DF_{Text(table, "name")}_{Text(column, "name")}", (Text(column, "default"), false));
            foreach (var index in table.GetProperty("indexes").EnumerateArray().Where(i => i.GetProperty("filter").ValueKind != JsonValueKind.Null))
                expected.Add(Text(index, "name"), (Text(index, "filter"), true));
        }

        Assert.Equal(expected.Keys.Order(), rows.Keys.Order());
        var differences = expected.Where(pair => SqlExpression.Normalize(pair.Value.expression, pair.Value.predicate) != SqlExpression.Normalize(rows[pair.Key], pair.Value.predicate)).Select(pair => pair.Key).ToList();
        Evidence("expression-differences.json", JsonSerializer.Serialize(differences.Select(name => new { name, source = SqlExpression.Normalize(expected[name].expression, expected[name].predicate), deployed = SqlExpression.Normalize(rows[name], expected[name].predicate) }), new JsonSerializerOptions { WriteIndented = true }));
        Evidence("deployed-expressions.json", JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        return differences;
    }

    private sealed record Finding(string Severity, string Rule, string Object, string Detail);
}
