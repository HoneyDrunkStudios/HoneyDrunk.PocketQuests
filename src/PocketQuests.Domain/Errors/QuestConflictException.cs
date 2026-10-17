namespace PocketQuests.Domain.Errors;

/// <summary>A quest action conflicts with the current account state.</summary>
/// <param name="message">The domain conflict.</param>
public sealed class QuestConflictException(string message) : InvalidOperationException(message);
