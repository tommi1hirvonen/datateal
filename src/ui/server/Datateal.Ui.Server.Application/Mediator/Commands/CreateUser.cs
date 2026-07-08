using Datateal.Core.Mediator;
using Datateal.Core.Users;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Users;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record CreateUserCommand(string Email, string DisplayName, List<string> Roles, bool HasAllCatalogAccess, List<Guid> CatalogIds)
    : IRequest<AppUserDto>;

internal class CreateUserHandler(IUserRepository repository) : IRequestHandler<CreateUserCommand, AppUserDto>
{
    public async Task<AppUserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await repository.EmailExistsAsync(request.Email, ct: cancellationToken))
            throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");

        var userId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        var user = new UserAccount
        {
            Id = userId,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Roles = request.Roles,
            HasAllCatalogAccess = request.HasAllCatalogAccess,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
            CatalogAccessList = request.CatalogIds
                .Select(catalogId => new UserCatalogAccess
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    CatalogId = catalogId,
                })
                .ToList(),
        };

        await repository.CreateAsync(user, cancellationToken);
        return UserDtoMapper.ToDto(user);
    }
}

internal static class UserDtoMapper
{
    internal static AppUserDto ToDto(UserAccount user) =>
        new(user.Id, user.Email, user.ExternalId, user.DisplayName,
            user.IsEnabled, user.HasAllCatalogAccess, user.Roles,
            user.CatalogAccessList.Select(a => new UserCatalogAccessDto(a.Id, a.CatalogId, a.Catalog?.Name ?? "")).ToList(),
            user.CreatedAt, user.UpdatedAt);

    internal static ServiceAccountDto ToServiceAccountDto(ServiceAccount account) =>
        new(account.Id, account.Email, account.DisplayName, account.Description,
            account.IsEnabled, account.HasAllCatalogAccess, account.Roles,
            account.CatalogAccessList.Select(a => new UserCatalogAccessDto(a.Id, a.CatalogId, a.Catalog?.Name ?? "")).ToList(),
            account.CreatedAt, account.UpdatedAt);
}
