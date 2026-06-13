using Datateal.Core.Kernels;
using Datateal.Ui.Shared.Catalogs;

namespace Datateal.Ui.Client.Services;

public interface ICatalogService
{
    Task<IReadOnlyList<CatalogDto>> GetCatalogsAsync(CancellationToken ct = default);
    /// <summary>All catalogs, unfiltered (requires CatalogManage). For management/grant screens.</summary>
    Task<IReadOnlyList<CatalogDto>> GetAllCatalogsAsync(CancellationToken ct = default);
    Task<CatalogWorkspaceAccessDto> GetWorkspaceAccessAsync(Guid catalogId, CancellationToken ct = default);
    Task SetWorkspaceAccessAsync(Guid catalogId, SetCatalogWorkspaceAccessRequest request, CancellationToken ct = default);
    Task<ManagedCatalogDto> CreateManagedCatalogAsync(CreateManagedCatalogRequest request, CancellationToken ct = default);
    Task<UnmanagedCatalogDto> CreateUnmanagedCatalogAsync(CreateUnmanagedCatalogRequest request, CancellationToken ct = default);
    Task<ManagedCatalogDto?> UpdateManagedCatalogAsync(Guid id, UpdateManagedCatalogRequest request, CancellationToken ct = default);
    Task<UnmanagedCatalogDto?> UpdateUnmanagedCatalogAsync(Guid id, UpdateUnmanagedCatalogRequest request, CancellationToken ct = default);
    Task DeleteCatalogAsync(Guid id, CancellationToken ct = default);
    Task<CatalogMetadataDto> GetMetadataAsync(Guid catalogId, CancellationToken ct = default);
    Task<CatalogInfoDto> GetCatalogInfoAsync(Guid catalogId, CancellationToken ct = default);

    Task<ExecutionHandle> SetupCatalogsOnKernelAsync(string nodeName, string kernelId, List<string> catalogNames, CancellationToken ct = default);
    Task<ExecutionHandle> ConnectCatalogOnKernelAsync(string nodeName, string kernelId, string catalogName, CancellationToken ct = default);
    Task<ExecutionHandle> DisconnectCatalogOnKernelAsync(string nodeName, string kernelId, string catalogName, CancellationToken ct = default);

    // Workspace item catalog associations
    Task<List<string>> GetWorkspaceItemCatalogsAsync(Guid itemId, CancellationToken ct = default);
    Task UpdateWorkspaceItemCatalogsAsync(Guid itemId, List<string> catalogNames, CancellationToken ct = default);
}
