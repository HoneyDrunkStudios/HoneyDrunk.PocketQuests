using PocketQuests.Contracts.Models.Accounts;

namespace PocketQuests.Services.Accounts;

/// <summary>Provides the authenticated account independently of HTTP or persistence.</summary>
public interface ICurrentAccount
{
    /// <summary>Gets the verified identity for the current operation.</summary>
    AccountIdentity Identity { get; }
}
