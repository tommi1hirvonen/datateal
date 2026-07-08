using Datateal.Auth;
using Datateal.Core.Catalogs;
using Datateal.Core.Users;
using Datateal.Core.Workspaces;
using Datateal.Data;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Core.Deployment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class AdminDeploymentService(
    DatatealDbContext db,
    ICatalogDatabaseService catalogDatabaseService,
    IOptions<CatalogSettings> catalogSettings) : IAdminDeploymentService
{
    public Task<ChangeSet> PlanAsync(Bundle bundle, CancellationToken ct = default) =>
        ProcessAsync(bundle, dryRun: true, ct);

    public Task<ChangeSet> ApplyAsync(Bundle bundle, CancellationToken ct = default) =>
        ProcessAsync(bundle, dryRun: false, ct);

    public async Task<Bundle> ExportAsync(CancellationToken ct = default)
    {
        var workspaces = await db.Workspaces
            .AsNoTracking()
            .OrderBy(w => w.Name)
            .ToListAsync(ct);
        var catalogs = await db.Catalogs
            .AsNoTracking()
            .Include(c => c.WorkspaceAccessList)
                .ThenInclude(access => access.Workspace)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        var memberships = await db.WorkspaceMemberships
            .AsNoTracking()
            .Include(m => m.Workspace)
            .Include(m => m.User)
            .OrderBy(m => m.Workspace.Name)
            .ThenBy(m => m.User.Email)
            .ToListAsync(ct);
        var users = await db.AppUsers
            .AsNoTracking()
            .Include(u => u.CatalogAccessList)
                .ThenInclude(access => access.Catalog)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var workspaceMapper = new AdminWorkspaceMapper();
        var catalogMapper = new AdminCatalogMapper();
        var membershipMapper = new AdminMembershipMapper();
        var userAccessMapper = new AdminUserCatalogAccessMapper();

        return new Bundle
        {
            Manifest = new BundleManifest
            {
                Scope = "admin",
            },
            Workspaces = workspaces.Select(workspaceMapper.ToModel).ToList(),
            Catalogs = catalogs
                .Select(catalog => catalogMapper.ToModel(
                    catalog,
                    catalog.WorkspaceAccessList.Select(access => access.Workspace.Name)))
                .ToList(),
            Memberships = memberships
                .GroupBy(membership => membership.Workspace.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => membershipMapper.ToModel(group.Key, group))
                .ToList(),
            UserCatalogAccess = users.Select(userAccessMapper.ToModel).ToList(),
        };
    }

    private async Task<ChangeSet> ProcessAsync(Bundle bundle, bool dryRun, CancellationToken ct)
    {
        ValidateAdminBundle(bundle);

        var workspaces = await db.Workspaces
            .AsNoTracking()
            .OrderBy(w => w.Name)
            .ToListAsync(ct);
        var catalogs = await db.Catalogs
            .AsNoTracking()
            .Include(c => c.WorkspaceAccessList)
                .ThenInclude(access => access.Workspace)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        var memberships = await db.WorkspaceMemberships
            .AsNoTracking()
            .Include(m => m.Workspace)
            .Include(m => m.User)
            .OrderBy(m => m.Workspace.Name)
            .ThenBy(m => m.User.Email)
            .ToListAsync(ct);
        var users = await db.AppUsers
            .AsNoTracking()
            .Include(u => u.CatalogAccessList)
                .ThenInclude(access => access.Catalog)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var workspaceMapper = new AdminWorkspaceMapper();
        var catalogMapper = new AdminCatalogMapper();
        var membershipMapper = new AdminMembershipMapper();
        var userAccessMapper = new AdminUserCatalogAccessMapper();

        var currentWorkspaceModels = workspaces.Select(workspaceMapper.ToModel).ToList();
        var currentCatalogModels = catalogs
            .Select(catalog => catalogMapper.ToModel(
                catalog,
                catalog.WorkspaceAccessList.Select(access => access.Workspace.Name)))
            .ToList();
        var currentMembershipModels = memberships
            .GroupBy(membership => membership.Workspace.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => membershipMapper.ToModel(group.Key, group))
            .ToList();
        var currentUserAccessModels = users.Select(userAccessMapper.ToModel).ToList();

        var workspaceDiff = DiffEngine.Diff(workspaceMapper, NormalizeWorkspaces(bundle.Workspaces), currentWorkspaceModels, allowDeletes: false);
        var catalogDiff = DiffEngine.Diff(catalogMapper, NormalizeCatalogs(bundle.Catalogs), currentCatalogModels, allowDeletes: false);
        var membershipDiff = DiffEngine.Diff(membershipMapper, NormalizeMemberships(bundle.Memberships), currentMembershipModels, allowDeletes: false);
        var userAccessDiff = DiffEngine.Diff(userAccessMapper, NormalizeUserAccess(bundle.UserCatalogAccess), currentUserAccessModels, allowDeletes: false);

        var changeSet = new ChangeSet
        {
            Scope = "admin",
            Target = "admin",
            DryRun = dryRun,
            Changes =
            [
                .. workspaceDiff.Changes,
                .. catalogDiff.Changes,
                .. membershipDiff.Changes,
                .. userAccessDiff.Changes,
            ],
        };

        if (dryRun)
            return changeSet;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await ApplyChangesAsync(workspaceDiff, catalogDiff, membershipDiff, userAccessDiff, ct);
            await transaction.CommitAsync(ct);
        });

        return changeSet;
    }

    private async Task ApplyChangesAsync(
        DiffResult<WorkspaceModel> workspaceDiff,
        DiffResult<CatalogModel> catalogDiff,
        DiffResult<WorkspaceMembershipModel> membershipDiff,
        DiffResult<UserCatalogAccessModel> userAccessDiff,
        CancellationToken ct)
    {
        var workspaces = await db.Workspaces.ToListAsync(ct);
        var catalogs = await db.Catalogs
            .Include(c => c.WorkspaceAccessList)
            .ToListAsync(ct);
        var users = await db.AppUsers
            .Include(u => u.CatalogAccessList)
            .ToListAsync(ct);
        var memberships = await db.WorkspaceMemberships.ToListAsync(ct);

        var workspaceByName = workspaces.ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);
        var catalogByName = catalogs.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var userByEmail = users.ToDictionary(u => u.Email, StringComparer.OrdinalIgnoreCase);
        var membershipByKey = memberships.ToDictionary(
            membership => (membership.WorkspaceId, membership.UserId));

        foreach (var workspace in NormalizeWorkspaces(workspaceDiff.Creations.Select(entry => entry.Model).ToList()))
        {
            if (workspaceByName.ContainsKey(workspace.Name))
                continue;

            var entity = new Workspace
            {
                Id = Guid.CreateVersion7(),
                Name = workspace.Name,
                Description = workspace.Description,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Workspaces.Add(entity);
            workspaces.Add(entity);
            workspaceByName[entity.Name] = entity;
        }

        foreach (var workspace in NormalizeWorkspaces(workspaceDiff.Updates.Select(entry => entry.Model).ToList()))
        {
            if (!workspaceByName.TryGetValue(workspace.Name, out var existing))
                continue;

            existing.Description = workspace.Description;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var catalog in NormalizeCatalogs(catalogDiff.Creations.Select(entry => entry.Model).ToList()))
            await UpsertCatalogAsync(catalog, workspaceByName, catalogByName, catalogs, ct);

        foreach (var catalog in NormalizeCatalogs(catalogDiff.Updates.Select(entry => entry.Model).ToList()))
            await UpsertCatalogAsync(catalog, workspaceByName, catalogByName, catalogs, ct);

        foreach (var membership in NormalizeMemberships(membershipDiff.Creations.Select(entry => entry.Model).ToList()))
            ApplyMembershipModel(membership, workspaceByName, userByEmail, membershipByKey);

        foreach (var membership in NormalizeMemberships(membershipDiff.Updates.Select(entry => entry.Model).ToList()))
            ApplyMembershipModel(membership, workspaceByName, userByEmail, membershipByKey);

        foreach (var userAccess in NormalizeUserAccess(userAccessDiff.Creations.Select(entry => entry.Model).ToList()))
            ApplyUserCatalogAccessModel(userAccess, catalogByName, userByEmail);

        foreach (var userAccess in NormalizeUserAccess(userAccessDiff.Updates.Select(entry => entry.Model).ToList()))
            ApplyUserCatalogAccessModel(userAccess, catalogByName, userByEmail);

        await db.SaveChangesAsync(ct);
    }

    private async Task UpsertCatalogAsync(
        CatalogModel model,
        Dictionary<string, Workspace> workspaceByName,
        Dictionary<string, Catalog> catalogByName,
        List<Catalog> catalogs,
        CancellationToken ct)
    {
        if (!catalogByName.TryGetValue(model.Name, out var existing))
        {
            existing = await CreateCatalogAsync(model, ct);
            db.Catalogs.Add(existing);
            catalogs.Add(existing);
            catalogByName[existing.Name] = existing;
        }
        else if (!string.Equals(
                     existing is ManagedCatalog ? "managed" : "unmanaged",
                     model.Type,
                     StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Catalog '{model.Name}' exists as a different type. Changing catalog type is not supported by admin deployment.");
        }

        existing.AccessibleFromAllWorkspaces = model.AccessibleFromAllWorkspaces ?? true;
        existing.UpdatedAt = DateTime.UtcNow;

        if (existing is UnmanagedCatalog unmanaged)
        {
            unmanaged.DataPath = model.DataPath ?? unmanaged.DataPath;
            unmanaged.CatalogHost = model.CatalogHost ?? unmanaged.CatalogHost;
            unmanaged.CatalogDatabase = model.CatalogDatabase ?? unmanaged.CatalogDatabase;
            unmanaged.CatalogUser = model.CatalogUser ?? unmanaged.CatalogUser;
        }

        if (existing.AccessibleFromAllWorkspaces)
            return;

        var existingWorkspaceIds = existing.WorkspaceAccessList
            .Select(access => access.WorkspaceId)
            .ToHashSet();

        foreach (var workspaceName in NormalizeNames(model.WorkspaceAccess))
        {
            if (!workspaceByName.TryGetValue(workspaceName, out var workspace))
                throw new InvalidOperationException($"Catalog '{model.Name}' references unknown workspace '{workspaceName}'.");

            if (existingWorkspaceIds.Add(workspace.Id))
            {
                existing.WorkspaceAccessList.Add(new CatalogWorkspaceAccess
                {
                    Id = Guid.CreateVersion7(),
                    CatalogId = existing.Id,
                    WorkspaceId = workspace.Id,
                });
            }
        }
    }

    private async Task<Catalog> CreateCatalogAsync(CatalogModel model, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (string.Equals(model.Type, "managed", StringComparison.OrdinalIgnoreCase))
        {
            var settings = catalogSettings.Value;
            await catalogDatabaseService.CreateDatabaseAsync(
                model.Name,
                settings.CatalogHost,
                settings.CatalogPort,
                settings.CatalogUser,
                settings.CatalogPassword,
                allowExistingDatabase: true,
                ct);

            return new ManagedCatalog
            {
                Id = Guid.CreateVersion7(),
                Name = model.Name,
                AccessibleFromAllWorkspaces = model.AccessibleFromAllWorkspaces ?? true,
                CreatedAt = now,
                UpdatedAt = now,
            };
        }

        return new UnmanagedCatalog
        {
            Id = Guid.CreateVersion7(),
            Name = model.Name,
            AccessibleFromAllWorkspaces = model.AccessibleFromAllWorkspaces ?? true,
            DataPath = model.DataPath ?? throw new InvalidOperationException(
                $"Unmanaged catalog '{model.Name}' is missing required field 'data_path'."),
            CatalogHost = model.CatalogHost ?? throw new InvalidOperationException(
                $"Unmanaged catalog '{model.Name}' is missing required field 'catalog_host'."),
            CatalogPort = 5432,
            CatalogDatabase = model.CatalogDatabase ?? throw new InvalidOperationException(
                $"Unmanaged catalog '{model.Name}' is missing required field 'catalog_database'."),
            CatalogUser = model.CatalogUser ?? throw new InvalidOperationException(
                $"Unmanaged catalog '{model.Name}' is missing required field 'catalog_user'."),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private void ApplyMembershipModel(
        WorkspaceMembershipModel model,
        Dictionary<string, Workspace> workspaceByName,
        Dictionary<string, AppUser> userByEmail,
        Dictionary<(Guid WorkspaceId, Guid UserId), WorkspaceMembership> membershipByKey)
    {
        if (!workspaceByName.TryGetValue(model.Workspace, out var workspace))
            throw new InvalidOperationException($"Memberships reference unknown workspace '{model.Workspace}'.");

        foreach (var member in model.Members)
        {
            var user = EnsureUserExists(member.Email, userByEmail);
            var roles = NormalizeRoles(member.Roles);
            foreach (var role in roles.Where(role => !DatatealRole.IsPerWorkspace(role)))
            {
                throw new InvalidOperationException(
                    $"Workspace membership for '{member.Email}' contains non-workspace role '{role}'.");
            }

            var key = (workspace.Id, user.Id);
            if (!membershipByKey.TryGetValue(key, out var membership))
            {
                membership = new WorkspaceMembership
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspace.Id,
                    UserId = user.Id,
                    Roles = roles,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                db.WorkspaceMemberships.Add(membership);
                membershipByKey[key] = membership;
                continue;
            }

            membership.Roles = roles;
            membership.UpdatedAt = DateTime.UtcNow;
        }
    }

    private void ApplyUserCatalogAccessModel(
        UserCatalogAccessModel model,
        Dictionary<string, Catalog> catalogByName,
        Dictionary<string, AppUser> userByEmail)
    {
        var user = EnsureUserExists(model.Email, userByEmail);
        user.HasAllCatalogAccess = model.HasAllCatalogAccess;
        user.UpdatedAt = DateTime.UtcNow;

        if (model.HasAllCatalogAccess)
            return;

        var existingCatalogIds = user.CatalogAccessList.Select(access => access.CatalogId).ToHashSet();
        foreach (var catalogName in NormalizeNames(model.AllowedCatalogs))
        {
            if (!catalogByName.TryGetValue(catalogName, out var catalog))
                throw new InvalidOperationException($"User '{model.Email}' references unknown catalog '{catalogName}'.");

            if (existingCatalogIds.Add(catalog.Id))
            {
                user.CatalogAccessList.Add(new UserCatalogAccess
                {
                    Id = Guid.CreateVersion7(),
                    UserId = user.Id,
                    CatalogId = catalog.Id,
                });
            }
        }
    }

    private AppUser EnsureUserExists(string email, Dictionary<string, AppUser> userByEmail)
    {
        if (userByEmail.TryGetValue(email, out var existing))
            return existing;

        var displayName = email.Contains('@', StringComparison.Ordinal)
            ? email[..email.IndexOf('@')]
            : email;

        var user = new UserAccount
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            DisplayName = displayName,
            ExternalId = null,
            IsEnabled = true,
            HasAllCatalogAccess = false,
            Roles = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.AppUsers.Add(user);
        userByEmail[email] = user;
        return user;
    }

    private static void ValidateAdminBundle(Bundle bundle)
    {
        if (!bundle.Manifest.Scope.Equals("admin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Expected an admin bundle but received '{bundle.Manifest.Scope}'.");
    }

    private static List<WorkspaceModel> NormalizeWorkspaces(List<WorkspaceModel> models) =>
        models
            .Select(model => new WorkspaceModel
            {
                Name = model.Name.Trim(),
                Description = model.Description,
            })
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<CatalogModel> NormalizeCatalogs(List<CatalogModel> models) =>
        models
            .Select(model => new CatalogModel
            {
                Name = model.Name.Trim(),
                Type = string.IsNullOrWhiteSpace(model.Type) ? "managed" : model.Type.Trim(),
                Description = model.Description,
                AccessibleFromAllWorkspaces = model.AccessibleFromAllWorkspaces,
                DataPath = model.DataPath,
                CatalogHost = model.CatalogHost,
                CatalogDatabase = model.CatalogDatabase,
                CatalogUser = model.CatalogUser,
                WorkspaceAccess = NormalizeNames(model.WorkspaceAccess),
            })
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<WorkspaceMembershipModel> NormalizeMemberships(List<WorkspaceMembershipModel> models) =>
        models
            .Select(model => new WorkspaceMembershipModel
            {
                Workspace = model.Workspace.Trim(),
                Members = model.Members
                    .Select(member => new WorkspaceMemberEntry
                    {
                        Email = member.Email.Trim(),
                        Roles = NormalizeRoles(member.Roles),
                    })
                    .OrderBy(member => member.Email, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            })
            .OrderBy(model => model.Workspace, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<UserCatalogAccessModel> NormalizeUserAccess(List<UserCatalogAccessModel> models) =>
        models
            .Select(model => new UserCatalogAccessModel
            {
                Email = model.Email.Trim(),
                HasAllCatalogAccess = model.HasAllCatalogAccess,
                AllowedCatalogs = NormalizeNames(model.AllowedCatalogs),
            })
            .OrderBy(model => model.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> NormalizeRoles(List<string>? roles) =>
        (roles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> NormalizeNames(List<string>? names) =>
        (names ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
