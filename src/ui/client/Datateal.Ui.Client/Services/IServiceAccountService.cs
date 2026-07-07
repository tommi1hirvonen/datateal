using Datateal.Ui.Shared.Users;

namespace Datateal.Ui.Client.Services;

public interface IServiceAccountService
{
    Task<IReadOnlyList<ServiceAccountDto>> GetServiceAccountsAsync(CancellationToken ct = default);
    Task<ServiceAccountDto> CreateServiceAccountAsync(CreateServiceAccountRequest request, CancellationToken ct = default);
    Task<ServiceAccountDto?> UpdateServiceAccountAsync(Guid id, UpdateServiceAccountRequest request, CancellationToken ct = default);
    Task DeleteServiceAccountAsync(Guid id, CancellationToken ct = default);
}
