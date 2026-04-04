using DuckHouse.Core.Kernels;

namespace DuckHouse.Ui.Server.Core.Repositories;

public interface IKernelRepository
{
    public Task<IReadOnlyList<KernelInfo>> GetKernelsAsync(string nodeName, CancellationToken cancellationToken = default);
}
