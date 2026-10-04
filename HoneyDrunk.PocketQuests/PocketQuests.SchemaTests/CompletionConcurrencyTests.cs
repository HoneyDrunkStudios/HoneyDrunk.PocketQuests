using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Relational.Entities;

namespace PocketQuests.SchemaTests;

/// <summary>Exercises the SQL uniqueness boundary with two independent writers in a separate scratch database.</summary>
/// <param name="fixture">A fresh schema fixture, distinct from metadata/probe tests.</param>
public sealed class CompletionConcurrencyTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    /// <summary>Only one concurrent completion commits; Undo releases the unique slot for exactly one recompletion.</summary>
    /// <returns>Completion after inspecting committed rows.</returns>
    [Fact]
    public async Task TwoConnectionsCannotCommitTwoLiveCompletions()
    {
        await fixture.Script("concurrent-completion-setup.sql");
        var first = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("60000000-0000-0000-0000-000000000002");
        var results = await Task.WhenAll(Attempt(first), Attempt(second));
        Assert.Single(results, result => result);
        await using var db = fixture.Context();
        var winner = await db.Set<QuestCompletionEntity>().SingleAsync();
        Assert.Null(winner.UndoneAt);
        await fixture.Execute("UPDATE pocketquests.QuestCompletion SET UndoneAt=DATEADD(minute,1,RecordedAt), UndoQuestOccurrenceEventId='60000000-0000-0000-0000-000000000003';");
        Assert.True(await Attempt(winner.Id == first ? second : first));
        Assert.Equal(2, await db.Set<QuestCompletionEntity>().CountAsync());
        Assert.Equal(1, await db.Set<QuestCompletionEntity>().CountAsync(row => row.UndoneAt == null));
    }

    private async Task<bool> Attempt(Guid id)
    {
        await using var connection = new SqlConnection(fixture.Connection);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            INSERT pocketquests.QuestCompletion(Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,RecordedAt)
            SELECT Id,AccountId,QuestOccurrenceId,QuestOccurrenceRevisionId,EffectiveAt
            FROM pocketquests.QuestOccurrenceEvent WHERE Id=@id;
            """,
            connection);
        command.Parameters.AddWithValue("@id", id);
        try
        {
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
            return true;
        }
        catch (SqlException exception) when (exception.Number == 2601)
        {
            Assert.Contains("UQ_QuestCompletion_OneLive", exception.Message, StringComparison.Ordinal);
            return false;
        }
    }
}
