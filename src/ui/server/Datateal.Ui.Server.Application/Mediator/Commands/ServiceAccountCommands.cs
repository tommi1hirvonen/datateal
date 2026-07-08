using Datateal.Core.Mediator;
using Datateal.Core.Users;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Users;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

// ── Create ──────────────────────────────────────────────────────────────────

public record CreateServiceAccountCommand(
    string Name,
    string? Description,
    List<string> Roles,
    bool HasAllCatalogAccess,
    List<Guid> CatalogIds) : IRequest<ServiceAccountDto>;

internal class CreateServiceAccountHandler(IUserRepository repository)
    : IRequestHandler<CreateServiceAccountCommand, ServiceAccountDto>
{
    public async Task<ServiceAccountDto> Handle(CreateServiceAccountCommand request, CancellationToken cancellationToken)
    {
        // Service accounts use the name as a unique label stored in the Email column.
        if (await repository.EmailExistsAsync(request.Name, ct: cancellationToken))
            throw new InvalidOperationException($"A user or service account with name '{request.Name}' already exists.");

        var id = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        var account = new ServiceAccount
        {
            Id = id,
            Email = request.Name,
            DisplayName = request.Name,
            Description = request.Description,
            Roles = request.Roles,
            HasAllCatalogAccess = request.HasAllCatalogAccess,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
            CatalogAccessList = request.CatalogIds
                .Select(catalogId => new UserCatalogAccess
                {
                    Id = Guid.CreateVersion7(),
                    UserId = id,
                    CatalogId = catalogId,
                })
                .ToList(),
        };

        await repository.CreateAsync(account, cancellationToken);
        return UserDtoMapper.ToServiceAccountDto(account);
    }
}

// ── Update ──────────────────────────────────────────────────────────────────

public record UpdateServiceAccountCommand(
    Guid Id,
    string Name,
    string? Description,
    bool IsEnabled,
    List<string> Roles,
    bool HasAllCatalogAccess,
    List<Guid> CatalogIds) : IRequest<ServiceAccountDto?>;

internal class UpdateServiceAccountHandler(IUserRepository repository)
    : IRequestHandler<UpdateServiceAccountCommand, ServiceAccountDto?>
{
    public async Task<ServiceAccountDto?> Handle(UpdateServiceAccountCommand request, CancellationToken cancellationToken)
    {
        var updated = await repository.UpdateServiceAccountAsync(
            request.Id, request.Name, request.Description, request.IsEnabled,
            request.Roles, request.HasAllCatalogAccess, request.CatalogIds,
            cancellationToken);
        return updated is not null ? UserDtoMapper.ToServiceAccountDto(updated) : null;
    }
}

// ── Delete ──────────────────────────────────────────────────────────────────

public record DeleteServiceAccountCommand(Guid Id) : IRequest<bool>;

internal class DeleteServiceAccountHandler(IUserRepository repository)
    : IRequestHandler<DeleteServiceAccountCommand, bool>
{
    public async Task<bool> Handle(DeleteServiceAccountCommand request, CancellationToken cancellationToken)
    {
        var activeTokenCount = await repository.GetActiveTokenCountByActingUserAsync(request.Id, cancellationToken);
        if (activeTokenCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete this service account: {activeTokenCount} active API token(s) reference it as the acting user. " +
                "Revoke or delete those tokens first.");

        return await repository.DeleteAsync(request.Id, cancellationToken);
    }
}
