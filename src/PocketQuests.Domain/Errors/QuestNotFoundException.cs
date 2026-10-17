namespace PocketQuests.Domain.Errors;

/// <summary>A requested account-owned quest resource does not exist.</summary>
/// <param name="message">The missing resource description.</param>
public sealed class QuestNotFoundException(string? message = null) : KeyNotFoundException(message);
