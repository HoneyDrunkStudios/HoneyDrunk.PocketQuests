using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PocketQuests.Data.Configurations;

/// <summary>Stores absolute instants in UTC; separate zone and offset columns preserve calendar semantics.</summary>
public sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(value => value.ToUniversalTime(), value => value.ToUniversalTime());
