using HoneyDrunk.Data.EntityFramework.Context;
using Microsoft.EntityFrameworkCore;

namespace PocketQuests.Data;

/// <summary>Common EF context behavior supplied by HoneyDrunk.Data.</summary>
/// <param name="options">Options supplied by the concrete context.</param>
public abstract class BaseDbContext(DbContextOptions options) : HoneyDrunkDbContext(options)
{
}
