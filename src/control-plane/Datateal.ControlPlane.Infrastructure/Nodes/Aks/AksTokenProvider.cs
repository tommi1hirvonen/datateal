using System.Net.Http.Headers;
using Azure.Core;
using k8s.Authentication;

namespace Datateal.ControlPlane.Infrastructure.Nodes.Aks;

// Bridges Azure.Core TokenCredential with the Kubernetes client's ITokenProvider.
// Tokens are cached and refreshed automatically by the underlying credential.
internal sealed class AksTokenProvider(TokenCredential credential) : ITokenProvider
{
    // Well-known app ID for the Azure Kubernetes Service AAD Server.
    private static readonly TokenRequestContext TokenRequestContext =
        new(["6dae42f8-4368-4678-94ff-3960e28e3630/.default"]);

    public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(TokenRequestContext, cancellationToken);
        return new AuthenticationHeaderValue("Bearer", token.Token);
    }
}
