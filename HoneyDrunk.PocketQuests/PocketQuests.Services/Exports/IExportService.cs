using PocketQuests.Contracts.Responses.Exports;

namespace PocketQuests.Services.Exports;

/// <summary>Private on-demand exports without retained artifacts.</summary>
public interface IExportService
{
    /// <summary>Builds one coherent source-derived snapshot.</summary>
    /// <param name="token">Cancellation.</param>
    /// <returns>The existing export contract.</returns>
    Task<QuestExport> Read(CancellationToken token = default);
}
