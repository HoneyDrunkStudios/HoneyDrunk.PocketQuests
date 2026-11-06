using PocketQuests.Contracts.Models.Administration;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Administration;

/// <summary>A bounded directory page; the next cursor retains the same search.</summary>
/// <param name="Users">The users included in this result.</param>
/// <param name="NextCursor">The cursor for the next result page, when present.</param>
public sealed record AdminUserPage(ImmutableArray<AdminUser> Users, string? NextCursor);
