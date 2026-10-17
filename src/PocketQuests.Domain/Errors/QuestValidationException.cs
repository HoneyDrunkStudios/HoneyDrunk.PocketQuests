namespace PocketQuests.Domain.Errors;

/// <summary>An explicitly rejected quest input with a message safe for the caller.</summary>
/// <param name="message">The actionable validation failure.</param>
public sealed class QuestValidationException(string message) : ArgumentException(message);
