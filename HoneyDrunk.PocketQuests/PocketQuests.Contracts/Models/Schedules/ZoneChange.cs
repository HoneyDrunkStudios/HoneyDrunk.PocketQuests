namespace PocketQuests.Contracts.Models.Schedules;

/// <summary>The public ZoneChange JSON contract, independent of storage and domain behavior.</summary>
public sealed record ZoneChange(string From, string To, DateTimeOffset At);
