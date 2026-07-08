using Datateal.Ui.Shared.ApiTokens;

namespace Datateal.Ui.Client.Services;

public interface IApiTokenService
{
    Task<IReadOnlyList<ApiTokenDto>> GetTokensAsync(CancellationToken ct = default);
    Task<CreateApiTokenResponse> CreateTokenAsync(CreateApiTokenRequest request, CancellationToken ct = default);
    Task RevokeTokenAsync(Guid id, CancellationToken ct = default);
    Task DeleteTokenAsync(Guid id, CancellationToken ct = default);
}
