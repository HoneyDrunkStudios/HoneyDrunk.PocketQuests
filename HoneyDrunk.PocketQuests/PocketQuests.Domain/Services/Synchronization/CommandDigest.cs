using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Quests;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace PocketQuests.Domain.Services.Synchronization;

// Digest v1 is an explicitly ordered tuple, independent of serializer property order/options.
// Adding a field, changing normalization or supported wire meanings requires a new digest version.
internal static class CommandDigest
{
    internal static byte[] Compute(QuestCommand command)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            writer.WriteStringValue("pocketquests-command/api-1/digest-1");
            Text(command.OperationId.ToString("D"));
            Text(command.Action);
            Text(command.OccurrenceId?.ToString("D"));
            Text(command.QuestId);
            Text(command.DueDate);
            Text(command.CompletionId?.ToString("D"));
            Quest(command.Definition);
            Number(command.ExpectedRevision);
            Text(command.SkillId);
            Number((int?)command.Experience);
            if (command.Interests is { } interests)
            {
                writer.WriteStartArray();
                foreach (var interest in interests)
                    Text(interest);
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNullValue();
            }

            Text(command.PlannedTime);
            Text(command.ParentId?.ToString("D"));
            Text(command.RewardId);
            Text(command.SeriesId?.ToString("D"));
            Number((int?)command.Cadence);
            Number(command.Interval);
            Text(command.CategoryId);
            writer.WriteBooleanValue(command.ConfirmPenalty);
            Number(command.AcceptedLoss);
            if (command.RecordedTime is { } proof)
            {
                if (!double.IsFinite(proof.ElapsedMilliseconds))
                    throw new QuestValidationException("Offline elapsed time must be finite.");
                writer.WriteStartArray();
                Text(proof.AnchorId.ToString("D"));
                Text(proof.BootId.ToString("D"));
                writer.WriteNumberValue(proof.Ordinal);
                writer.WriteNumberValue(proof.ElapsedMilliseconds);
                Text(proof.DeviceUtc.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNullValue();
            }

            Text(command.SkillName);
            Quest(command.AcceptedQuest);
            Text(command.NewZone);
            Text(command.ExpectedZone);
            writer.WriteBooleanValue(command.ConfirmZoneChange);
            if (command.ExpiryWarnings is { } warnings)
                writer.WriteBooleanValue(warnings);
            else
                writer.WriteNullValue();
            writer.WriteEndArray();

            void Text(string? value) => writer.WriteStringValue(value);
            void Number(int? value)
            {
                if (value is { } number)
                    writer.WriteNumberValue(number);
                else
                    writer.WriteNullValue();
            }

            void Shares(ImmutableArray<Share> shares)
            {
                writer.WriteStartArray();
                foreach (var share in shares)
                {
                    writer.WriteStartArray();
                    Text(share.Id);
                    writer.WriteNumberValue(share.BasisPoints);
                    writer.WriteEndArray();
                }

                writer.WriteEndArray();
            }

            void Quest(Quest? quest)
            {
                if (quest is null)
                {
                    writer.WriteNullValue();
                    return;
                }

                writer.WriteStartArray();
                Text(quest.Id);
                Text(quest.Title);
                Text(quest.Criterion);
                Text(quest.CategoryId);
                Number((int)quest.Rank);
                Number((int)quest.Effort);
                Shares(quest.Attributes);
                Shares(quest.Skills);
                writer.WriteBooleanValue(quest.IsCustom);
                Text(quest.Description);
                Number(quest.PenaltyPercent);
                writer.WriteEndArray();
            }
        }

        return SHA256.HashData(stream.ToArray());
    }
}
