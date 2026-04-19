using DuckHouse.Core.Kernels;
using DuckHouse.Ui.Shared.Catalogs;

namespace DuckHouse.Ui.Client.Services;

public interface ICatalogService
{
    Task<IReadOnlyList<CatalogDto>> GetCatalogsAsync(CancellationToken ct = default);
    Task<ManagedCatalogDto> CreateManagedCatalogAsync(CreateManagedCatalogRequest request, CancellationToken ct = default);
    Task<UnmanagedCatalogDto> CreateUnmanagedCatalogAsync(CreateUnmanagedCatalogRequest request, CancellationToken ct = default);
    Task<ManagedCatalogDto?> UpdateManagedCatalogAsync(Guid id, UpdateManagedCatalogRequest request, CancellationToken ct = default);
    Task<UnmanagedCatalogDto?> UpdateUnmanagedCatalogAsync(Guid id, UpdateUnmanagedCatalogRequest request, CancellationToken ct = default);
    Task DeleteCatalogAsync(Guid id, CancellationToken ct = default);
    Task<CatalogMetadataDto> GetMetadataAsync(Guid catalogId, CancellationToken ct = default);

    Task<ExecutionHandle> SetupCatalogsOnKernelAsync(string nodeName, string kernelId, List<string> catalogNames, CancellationToken ct = default);
    Task<ExecutionHandle> ConnectCatalogOnKernelAsync(string nodeName, string kernelId, string catalogName, CancellationToken ct = default);
    Task<ExecutionHandle> DisconnectCatalogOnKernelAsync(string nodeName, string kernelId, string catalogName, CancellationToken ct = default);

    // Workspace item catalog associations
    Task<List<string>> GetWorkspaceItemCatalogsAsync(Guid itemId, CancellationToken ct = default);
    Task UpdateWorkspaceItemCatalogsAsync(Guid itemId, List<string> catalogNames, CancellationToken ct = default);
}
