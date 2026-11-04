namespace PocketQuests.Services.Administration;

/// <summary>Current product access for a canonical user already verified by Identity.</summary>
public interface IAccountAccessService
{
    /// <summary>Checks suspension without creating or mutating a profile.</summary>
    /// <param name="userId">Canonical Identity-verified user identifier.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Whether the product profile is currently suspended.</returns>
    Task<bool> IsSuspended(string userId, CancellationToken token);
}
