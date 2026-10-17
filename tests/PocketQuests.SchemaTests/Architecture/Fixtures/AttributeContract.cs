using System.Text.Json.Serialization;

namespace PocketQuests.SchemaTests.Architecture.Fixtures;

[JsonDerivedType(typeof(PocketQuests.Data.Entities.Accounts.AccountEntity))]
internal abstract class AttributeContract;
