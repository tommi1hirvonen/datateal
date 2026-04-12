using DuckHouse.Ui.Shared.Packages;

namespace DuckHouse.Ui.Client.Services;

public interface IWheelPackageService
{
    Task<IReadOnlyList<WheelPackageDto>> GetPackagesAsync(CancellationToken ct = default);
    Task<WheelPackageDto> UploadPackageAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file, CancellationToken ct = default);
    Task DeletePackageAsync(Guid id, CancellationToken ct = default);
}
