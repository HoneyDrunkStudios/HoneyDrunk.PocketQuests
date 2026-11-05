using PocketQuests.Contracts.Models.Administration;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Administration;

/// <summary>A bounded directory page; the next cursor retains the same search.</summary>
public sealed record AdminUserPage(ImmutableArray<AdminUser> Users, string? NextCursor);
