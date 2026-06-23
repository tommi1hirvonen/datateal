using Datateal.Ui.Shared.Ai;
using Microsoft.Extensions.AI;

namespace Datateal.Ui.Server.Application.Ai;

/// <summary>
/// Factory that creates provider-specific <see cref="IChatClient"/> instances.
/// Defined in Application layer; implemented in Infrastructure.
/// </summary>
public interface IAiProviderFactory
{
    /// <summary>
    /// Creates an <see cref="IChatClient"/> for the specified provider using the given credentials.
    /// The returned client is transient — it is not cached or reused across requests.
    /// </summary>
    IChatClient Create(AiProviderType provider, string apiKey, string endpoint, string model);
}
