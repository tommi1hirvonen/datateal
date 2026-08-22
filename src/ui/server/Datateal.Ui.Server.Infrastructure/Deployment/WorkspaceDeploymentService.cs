using System.Text;
using Datateal.Core.Deployment;
using Datateal.Core.Environment;
using Datateal.Core.RuntimePackages;
using Datateal.Core.Workspace;
using Datateal.Data;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Ui.Server.Core.Deployment;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WorkspaceSecret = Datateal.Core.Environment.Secret;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class WorkspaceDeploymentService(
    DatatealDbContext db,
    IDataProtectionProvider dataProtection) : IWorkspaceDeploymentService
{
    private readonly IDataProtector _secretProtector = dataProtection.CreateProtector("Datateal.Secrets");

    public Task<ChangeSet> PlanAsync(Guid workspaceId, Bundle bundle, IReadOnlyDictionary<string, string>? env = null, CancellationToken ct = default) =>
        ProcessAsync(workspaceId, bundle, dryRun: true, env, ct);

    public Task<ChangeSet> ApplyAsync(Guid workspaceId, Bundle bundle, IReadOnlyDictionary<string, string>? env = null, CancellationToken ct = default) =>
        ProcessAsync(workspaceId, bundle, dryRun: false, env, ct);

    public async Task<Bundle> ExportAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var workspace = await db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' was not found.");

        var folders = await db.Folders
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var items = await db.WorkspaceItems
            .AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var nodePools = await db.NodePoolConfigs
            .AsNoTracking()
            .Where(p => p.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var variables = await db.EnvironmentVariables
            .AsNoTracking()
            .Where(v => v.WorkspaceId == workspaceId)
            .OrderBy(v => v.Key)
            .ToListAsync(ct);
        var secrets = await db.Secrets
            .AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderBy(s => s.Key)
            .ToListAsync(ct);
        var wheelPackages = await db.WheelPackages
            .AsNoTracking()
            .Where(w => w.WorkspaceId == workspaceId)
            .OrderBy(w => w.Name)
            .ToListAsync(ct);

        var folderMapper = new WorkspaceFolderMapper();
        var notebookMapper = new WorkspaceNotebookMapper();
        var queryMapper = new WorkspaceQueryMapper();
        var nodePoolMapper = new WorkspaceNodePoolMapper();
        var variableMapper = new WorkspaceEnvironmentVariableMapper();
        var secretMapper = new WorkspaceSecretMapper();
        var wheelMapper = new WorkspaceWheelPackageMapper();

        var folderPaths = DeploymentPathHelpers.BuildFolderPathMap(folders);
        var bundleFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        var notebookModels = new List<NotebookModel>();
        var queryModels = new List<QueryModel>();
        foreach (var item in items)
        {
            var logicalPath = DeploymentPathHelpers.GetItemPath(item, folderPaths);
            switch (item)
            {
                case Notebook notebook:
                    notebookModels.Add(notebookMapper.ToModel(notebook, logicalPath));
                    bundleFiles[DeploymentPathHelpers.GetNotebookSourceFile(logicalPath)] = Encoding.UTF8.GetBytes(notebook.Content);
                    break;
                case Query query:
                    queryModels.Add(queryMapper.ToModel(query, logicalPath));
                    bundleFiles[DeploymentPathHelpers.GetQuerySourceFile(logicalPath)] = Encoding.UTF8.GetBytes(query.Content);
                    break;
            }
        }

        foreach (var package in wheelPackages)
            bundleFiles[DeploymentPathHelpers.GetWheelBundleFilePath(package.FileName)] = package.Data;

        var wheelNamesById = wheelPackages.ToDictionary(w => w.Id, w => w.Name);
        var variableNamesById = variables.ToDictionary(v => v.Id, v => v.Key);
        var secretNamesById = secrets.ToDictionary(s => s.Id, s => s.Key);

        return new Bundle
        {
            Manifest = new BundleManifest
            {
                Scope = "workspace",
                TargetWorkspace = workspace.Name,
            },
            Folders = folders
                .Select(folder => folderMapper.ToModel(folder, folderPaths[folder.Id]))
                .OrderBy(model => model.Path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Notebooks = notebookModels.OrderBy(model => model.Path, StringComparer.OrdinalIgnoreCase).ToList(),
            Queries = queryModels.OrderBy(model => model.Path, StringComparer.OrdinalIgnoreCase).ToList(),
            NodePools = nodePools
                .Select(pool => nodePoolMapper.ToModel(pool, wheelNamesById, variableNamesById, secretNamesById))
                .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            EnvironmentVariables = variables.Select(variableMapper.ToModel).ToList(),
            Secrets = secrets.Select(secretMapper.ToModel).ToList(),
            WheelPackages = wheelPackages.Select(wheelMapper.ToModel).ToList(),
            Files = bundleFiles,
        };
    }

    public async Task<WorkspaceDeploymentSnapshot> CreateSnapshotAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var bundle = await ExportAsync(workspaceId, ct);
        var encryptedSecrets = await db.Secrets
            .AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId)
            .ToDictionaryAsync(s => s.Key, s => s.EncryptedValue, StringComparer.OrdinalIgnoreCase, ct);

        return new WorkspaceDeploymentSnapshot(bundle, encryptedSecrets);
    }

    public async Task RestoreSnapshotAsync(Guid workspaceId, WorkspaceDeploymentSnapshot snapshot, CancellationToken ct = default)
    {
        var bundle = snapshot.Bundle;
        ValidateWorkspaceBundle(bundle);

        var workspace = await db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' was not found.");

        ValidateBundleFiles(bundle);

        var folders = await db.Folders
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var items = await db.WorkspaceItems
            .AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var nodePools = await db.NodePoolConfigs
            .AsNoTracking()
            .Where(p => p.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var variables = await db.EnvironmentVariables
            .AsNoTracking()
            .Where(v => v.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var secrets = await db.Secrets
            .AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var wheelPackages = await db.WheelPackages
            .AsNoTracking()
            .Where(w => w.WorkspaceId == workspaceId)
            .ToListAsync(ct);

        var folderMapper = new WorkspaceFolderMapper();
        var notebookMapper = new WorkspaceNotebookMapper();
        var queryMapper = new WorkspaceQueryMapper();
        var nodePoolMapper = new WorkspaceNodePoolMapper();
        var variableMapper = new WorkspaceEnvironmentVariableMapper();
        var secretMapper = new WorkspaceSecretMapper();
        var wheelMapper = new WorkspaceWheelPackageMapper();

        notebookMapper.LoadDesired(bundle);
        queryMapper.LoadDesired(bundle);
        wheelMapper.LoadDesired(bundle);

        var folderPaths = DeploymentPathHelpers.BuildFolderPathMap(folders);
        var wheelNamesById = wheelPackages.ToDictionary(w => w.Id, w => w.Name);
        var variableNamesById = variables.ToDictionary(v => v.Id, v => v.Key);
        var secretNamesById = secrets.ToDictionary(s => s.Id, s => s.Key);

        var currentNotebookModels = items
            .OfType<Notebook>()
            .Select(notebook => notebookMapper.ToModel(notebook, DeploymentPathHelpers.GetItemPath(notebook, folderPaths)))
            .ToList();
        var currentQueryModels = items
            .OfType<Query>()
            .Select(query => queryMapper.ToModel(query, DeploymentPathHelpers.GetItemPath(query, folderPaths)))
            .ToList();
        var currentFolderModels = folders
            .Select(folder => folderMapper.ToModel(folder, folderPaths[folder.Id]))
            .ToList();
        var currentNodePoolModels = nodePools
            .Select(pool => nodePoolMapper.ToModel(pool, wheelNamesById, variableNamesById, secretNamesById))
            .ToList();
        var currentVariableModels = variables.Select(variableMapper.ToModel).ToList();
        var currentSecretModels = secrets.Select(secretMapper.ToModel).ToList();
        var currentWheelModels = wheelPackages.Select(wheelMapper.ToModel).ToList();

        var folderDiff = DiffEngine.Diff(folderMapper, NormalizeFolders(bundle.Folders), currentFolderModels, allowDeletes: true);
        var notebookDiff = DiffEngine.Diff(notebookMapper, NormalizeNotebooks(bundle.Notebooks), currentNotebookModels, allowDeletes: true);
        var queryDiff = DiffEngine.Diff(queryMapper, NormalizeQueries(bundle.Queries), currentQueryModels, allowDeletes: true);
        var nodePoolDiff = DiffEngine.Diff(nodePoolMapper, NormalizeNodePools(bundle.NodePools), currentNodePoolModels, allowDeletes: true);
        var variableDiff = DiffEngine.Diff(variableMapper, NormalizeVariables(bundle.EnvironmentVariables), currentVariableModels, allowDeletes: true);
        var secretDiff = DiffEngine.Diff(secretMapper, NormalizeSecrets(bundle.Secrets), currentSecretModels, allowDeletes: true);
        var wheelDiff = DiffEngine.Diff(wheelMapper, NormalizeWheelPackages(bundle.WheelPackages), currentWheelModels, allowDeletes: true);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await ApplyChangesAsync(workspaceId, bundle, folderDiff, notebookDiff, queryDiff, nodePoolDiff, variableDiff, secretDiff, wheelDiff, env: null, snapshot.EncryptedSecretsByKey, ct);
            await transaction.CommitAsync(ct);
        });
    }

    public async Task<Guid> CreateDeploymentLogAsync(
        Guid workspaceId,
        DeploymentScope scope,
        string targetBundleJson,
        string snapshotJson,
        string? issuedByUserId = null,
        string? issuedByDisplayName = null,
        CancellationToken ct = default)
    {
        var log = DeploymentLog.Create(workspaceId, scope, targetBundleJson, snapshotJson, issuedByUserId, issuedByDisplayName);
        db.DeploymentLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return log.Id;
    }

    public async Task UpdateDeploymentLogStatusAsync(
        Guid logId,
        DeploymentStatus status,
        string? failureReason = null,
        CancellationToken ct = default)
    {
        var log = await db.DeploymentLogs.FirstOrDefaultAsync(l => l.Id == logId, ct)
            ?? throw new InvalidOperationException($"Deployment log '{logId}' was not found.");

        switch (status)
        {
            case DeploymentStatus.ApplyingUi:
                log.TransitionToApplyingUi();
                break;
            case DeploymentStatus.ApplyingJobs:
                log.TransitionToApplyingJobs();
                break;
            case DeploymentStatus.Completed:
                log.TransitionToCompleted();
                break;
            case DeploymentStatus.RollingBack:
                log.TransitionToRollingBack(failureReason ?? "Rollback initiated.");
                break;
            case DeploymentStatus.RolledBack:
                log.TransitionToRolledBack();
                break;
            case DeploymentStatus.Failed:
                log.TransitionToFailed(failureReason ?? "Deployment operation failed.");
                break;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<ChangeSet> ProcessAsync(Guid workspaceId, Bundle bundle, bool dryRun, IReadOnlyDictionary<string, string>? env, CancellationToken ct)
    {
        ValidateWorkspaceBundle(bundle);

        var workspace = await db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' was not found.");

        if (!string.IsNullOrWhiteSpace(bundle.Manifest.TargetWorkspace)
            && !string.Equals(bundle.Manifest.TargetWorkspace, workspace.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Bundle targets workspace '{bundle.Manifest.TargetWorkspace}', but the requested workspace is '{workspace.Name}'.");
        }

        ValidateBundleFiles(bundle);

        var folders = await db.Folders
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var items = await db.WorkspaceItems
            .AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var nodePools = await db.NodePoolConfigs
            .AsNoTracking()
            .Where(p => p.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var variables = await db.EnvironmentVariables
            .AsNoTracking()
            .Where(v => v.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var secrets = await db.Secrets
            .AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var wheelPackages = await db.WheelPackages
            .AsNoTracking()
            .Where(w => w.WorkspaceId == workspaceId)
            .ToListAsync(ct);
        var folderMapper = new WorkspaceFolderMapper();
        var notebookMapper = new WorkspaceNotebookMapper();
        var queryMapper = new WorkspaceQueryMapper();
        var nodePoolMapper = new WorkspaceNodePoolMapper();
        var variableMapper = new WorkspaceEnvironmentVariableMapper();
        var secretMapper = new WorkspaceSecretMapper();
        var wheelMapper = new WorkspaceWheelPackageMapper();

        notebookMapper.LoadDesired(bundle);
        queryMapper.LoadDesired(bundle);
        wheelMapper.LoadDesired(bundle);

        var folderPaths = DeploymentPathHelpers.BuildFolderPathMap(folders);
        var wheelNamesById = wheelPackages.ToDictionary(w => w.Id, w => w.Name);
        var variableNamesById = variables.ToDictionary(v => v.Id, v => v.Key);
        var secretNamesById = secrets.ToDictionary(s => s.Id, s => s.Key);

        var currentNotebookModels = items
            .OfType<Notebook>()
            .Select(notebook => notebookMapper.ToModel(notebook, DeploymentPathHelpers.GetItemPath(notebook, folderPaths)))
            .ToList();
        var currentQueryModels = items
            .OfType<Query>()
            .Select(query => queryMapper.ToModel(query, DeploymentPathHelpers.GetItemPath(query, folderPaths)))
            .ToList();

        WorkspaceBundleValidator.Validate(bundle);

        var currentFolderModels = folders
            .Select(folder => folderMapper.ToModel(folder, folderPaths[folder.Id]))
            .ToList();
        var currentNodePoolModels = nodePools
            .Select(pool => nodePoolMapper.ToModel(pool, wheelNamesById, variableNamesById, secretNamesById))
            .ToList();
        var currentVariableModels = variables.Select(variableMapper.ToModel).ToList();
        var currentSecretModels = secrets.Select(secretMapper.ToModel).ToList();
        var currentWheelModels = wheelPackages.Select(wheelMapper.ToModel).ToList();

        var folderDiff = DiffEngine.Diff(folderMapper, NormalizeFolders(bundle.Folders), currentFolderModels, allowDeletes: true);
        var notebookDiff = DiffEngine.Diff(notebookMapper, NormalizeNotebooks(bundle.Notebooks), currentNotebookModels, allowDeletes: true);
        var queryDiff = DiffEngine.Diff(queryMapper, NormalizeQueries(bundle.Queries), currentQueryModels, allowDeletes: true);
        var nodePoolDiff = DiffEngine.Diff(nodePoolMapper, NormalizeNodePools(bundle.NodePools), currentNodePoolModels, allowDeletes: true);
        var variableDiff = DiffEngine.Diff(variableMapper, NormalizeVariables(bundle.EnvironmentVariables), currentVariableModels, allowDeletes: true);
        var secretDiff = DiffEngine.Diff(secretMapper, NormalizeSecrets(bundle.Secrets), currentSecretModels, allowDeletes: true);
        var wheelDiff = DiffEngine.Diff(wheelMapper, NormalizeWheelPackages(bundle.WheelPackages), currentWheelModels, allowDeletes: true);

        ValidateSecretCreates(secretDiff);

        var changeSet = new ChangeSet
        {
            Scope = "workspace",
            Target = workspace.Name,
            DryRun = dryRun,
            Changes =
            [
                .. folderDiff.Changes,
                .. notebookDiff.Changes,
                .. queryDiff.Changes,
                .. nodePoolDiff.Changes,
                .. variableDiff.Changes,
                .. secretDiff.Changes,
                .. wheelDiff.Changes,
            ],
        };

        if (dryRun)
            return changeSet;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await ApplyChangesAsync(workspaceId, bundle, folderDiff, notebookDiff, queryDiff, nodePoolDiff, variableDiff, secretDiff, wheelDiff, env, secretEncryptedValuesOverride: null, ct);
            await transaction.CommitAsync(ct);
        });

        return changeSet;
    }

    private async Task ApplyChangesAsync(
        Guid workspaceId,
        Bundle bundle,
        DiffResult<FolderModel> folderDiff,
        DiffResult<NotebookModel> notebookDiff,
        DiffResult<QueryModel> queryDiff,
        DiffResult<NodePoolModel> nodePoolDiff,
        DiffResult<EnvironmentVariableModel> variableDiff,
        DiffResult<SecretModel> secretDiff,
        DiffResult<WheelPackageModel> wheelDiff,
        IReadOnlyDictionary<string, string>? env,
        IReadOnlyDictionary<string, string>? secretEncryptedValuesOverride,
        CancellationToken ct)
    {
        var folders = await db.Folders.Where(f => f.WorkspaceId == workspaceId).ToListAsync(ct);
        var items = await db.WorkspaceItems.Where(i => i.WorkspaceId == workspaceId).ToListAsync(ct);
        var nodePools = await db.NodePoolConfigs.Where(p => p.WorkspaceId == workspaceId).ToListAsync(ct);
        var variables = await db.EnvironmentVariables.Where(v => v.WorkspaceId == workspaceId).ToListAsync(ct);
        var secrets = await db.Secrets.Where(s => s.WorkspaceId == workspaceId).ToListAsync(ct);
        var wheelPackages = await db.WheelPackages.Where(w => w.WorkspaceId == workspaceId).ToListAsync(ct);

        var folderByPath = BuildFolderLookup(folders);
        var notebookByPath = BuildNotebookLookup(items, folders);
        var queryByPath = BuildQueryLookup(items, folders);
        var variableByKey = variables.ToDictionary(v => v.Key, StringComparer.OrdinalIgnoreCase);
        var secretByKey = secrets.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        var wheelByName = wheelPackages.ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);
        var nodePoolByName = nodePools.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (model, _) in notebookDiff.Deletions)
        {
            if (notebookByPath.TryGetValue(model.Path, out var notebook))
            {
                db.WorkspaceItems.Remove(notebook);
                items.Remove(notebook);
                notebookByPath.Remove(model.Path);
            }
        }

        foreach (var (model, _) in queryDiff.Deletions)
        {
            if (queryByPath.TryGetValue(model.Path, out var query))
            {
                db.WorkspaceItems.Remove(query);
                items.Remove(query);
                queryByPath.Remove(model.Path);
            }
        }

        await db.SaveChangesAsync(ct);

        ApplyEnvironmentVariables(workspaceId, bundle.Manifest.Variables, variableDiff, variables, variableByKey, env);
        ApplySecrets(workspaceId, bundle.Manifest.Variables, secretDiff, secrets, secretByKey, env, secretEncryptedValuesOverride);
        ApplyWheelPackages(workspaceId, bundle, wheelDiff, wheelPackages, wheelByName);
        await ApplyNodePools(workspaceId, nodePoolDiff, nodePools, nodePoolByName, wheelByName, variableByKey, secretByKey, ct);

        foreach (var folder in NormalizeFolders(folderDiff.Creations.Select(entry => entry.Model).ToList())
                     .OrderBy(model => DeploymentPathHelpers.GetDepth(model.Path))
                     .ThenBy(model => model.Path, StringComparer.OrdinalIgnoreCase))
        {
            EnsureFolderExists(workspaceId, folder.Path, folders, folderByPath);
        }

        foreach (var notebook in NormalizeNotebooks(notebookDiff.Creations.Select(entry => entry.Model).ToList()))
            ApplyNotebook(workspaceId, bundle, notebook, folderByPath, notebookByPath, items, isCreate: true);

        foreach (var notebook in NormalizeNotebooks(notebookDiff.Updates.Select(entry => entry.Model).ToList()))
            ApplyNotebook(workspaceId, bundle, notebook, folderByPath, notebookByPath, items, isCreate: false);

        foreach (var query in NormalizeQueries(queryDiff.Creations.Select(entry => entry.Model).ToList()))
            ApplyQuery(workspaceId, bundle, query, folderByPath, queryByPath, items, isCreate: true);

        foreach (var query in NormalizeQueries(queryDiff.Updates.Select(entry => entry.Model).ToList()))
            ApplyQuery(workspaceId, bundle, query, folderByPath, queryByPath, items, isCreate: false);

        foreach (var (model, _) in nodePoolDiff.Deletions)
        {
            if (nodePoolByName.TryGetValue(model.Name, out var pool))
            {
                db.NodePoolConfigs.Remove(pool);
                nodePools.Remove(pool);
                nodePoolByName.Remove(model.Name);
            }
        }

        foreach (var folder in folderDiff.Deletions
                     .Select(entry => entry.Model)
                     .OrderByDescending(model => DeploymentPathHelpers.GetDepth(model.Path))
                     .ThenBy(model => model.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (folderByPath.TryGetValue(folder.Path, out var trackedFolder))
            {
                db.Folders.Remove(trackedFolder);
                folders.Remove(trackedFolder);
                folderByPath.Remove(folder.Path);
            }
        }

        foreach (var (model, _) in variableDiff.Deletions)
        {
            if (variableByKey.TryGetValue(model.Key, out var variable))
            {
                db.EnvironmentVariables.Remove(variable);
                variables.Remove(variable);
                variableByKey.Remove(model.Key);
            }
        }

        foreach (var (model, _) in secretDiff.Deletions)
        {
            if (secretByKey.TryGetValue(model.Key, out var secret))
            {
                db.Secrets.Remove(secret);
                secrets.Remove(secret);
                secretByKey.Remove(model.Key);
            }
        }

        foreach (var (model, _) in wheelDiff.Deletions)
        {
            if (wheelByName.TryGetValue(model.Name, out var wheel))
            {
                db.WheelPackages.Remove(wheel);
                wheelPackages.Remove(wheel);
                wheelByName.Remove(model.Name);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyNodePools(
        Guid workspaceId,
        DiffResult<NodePoolModel> nodePoolDiff,
        List<NodePoolConfig> nodePools,
        Dictionary<string, NodePoolConfig> nodePoolByName,
        Dictionary<string, WheelPackage> wheelByName,
        Dictionary<string, EnvironmentVariable> variableByKey,
        Dictionary<string, WorkspaceSecret> secretByKey,
        CancellationToken ct)
    {
        foreach (var nodePool in NormalizeNodePools(nodePoolDiff.Creations.Select(entry => entry.Model).ToList()))
        {
            var entity = CreateNodePool(workspaceId, nodePool, wheelByName, variableByKey, secretByKey);
            db.NodePoolConfigs.Add(entity);
            nodePools.Add(entity);
            nodePoolByName[nodePool.Name] = entity;
        }

        foreach (var nodePool in NormalizeNodePools(nodePoolDiff.Updates.Select(entry => entry.Model).ToList()))
        {
            if (!nodePoolByName.TryGetValue(nodePool.Name, out var existing))
            {
                var created = CreateNodePool(workspaceId, nodePool, wheelByName, variableByKey, secretByKey);
                db.NodePoolConfigs.Add(created);
                nodePools.Add(created);
                nodePoolByName[nodePool.Name] = created;
                continue;
            }

            var desiredType = string.Equals(nodePool.Type, "interactive", StringComparison.OrdinalIgnoreCase)
                ? typeof(InteractiveNodePoolConfig)
                : typeof(JobNodePoolConfig);

            if (existing.GetType() != desiredType)
            {
                db.NodePoolConfigs.Remove(existing);
                nodePools.Remove(existing);
                nodePoolByName.Remove(nodePool.Name);
                await db.SaveChangesAsync(ct);

                var replacement = CreateNodePool(workspaceId, nodePool, wheelByName, variableByKey, secretByKey);
                db.NodePoolConfigs.Add(replacement);
                nodePools.Add(replacement);
                nodePoolByName[nodePool.Name] = replacement;
                continue;
            }

            existing.VmSize = nodePool.VmSize;
            existing.Description = nodePool.Description;
            existing.KernelRequirements = nodePool.KernelRequirements;
            existing.NodeIdleTimeout = ParseNullableTimeSpan(nodePool.NodeIdleTimeout, $"node pool '{nodePool.Name}' node_idle_timeout");
            existing.WheelPackageIds = ResolveIds(nodePool.WheelPackages, wheelByName, "wheel package");
            existing.EnvironmentVariableIds = ResolveIds(nodePool.EnvironmentVariables, variableByKey, "environment variable");
            existing.SecretIds = ResolveIds(nodePool.Secrets, secretByKey, "secret");
            existing.UpdatedAt = DateTime.UtcNow;

            if (existing is JobNodePoolConfig jobPool)
            {
                jobPool.WarmNodes = nodePool.WarmNodes ?? 0;
                jobPool.MaxNodes = nodePool.MaxNodes;
                jobPool.NodeAcquireTimeout = ParseNullableTimeSpan(nodePool.NodeAcquireTimeout, $"node pool '{nodePool.Name}' node_acquire_timeout");
            }
        }
    }

    private void ApplyEnvironmentVariables(
        Guid workspaceId,
        IReadOnlyDictionary<string, string>? deploymentVariables,
        DiffResult<EnvironmentVariableModel> variableDiff,
        List<EnvironmentVariable> variables,
        Dictionary<string, EnvironmentVariable> variableByKey,
        IReadOnlyDictionary<string, string>? env)
    {
        foreach (var variable in NormalizeVariables(variableDiff.Creations.Select(entry => entry.Model).ToList()))
        {
            var entity = new EnvironmentVariable
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                Key = variable.Key,
                Value = VariableSubstitution.Substitute(variable.Value, deploymentVariables, env),
                Description = variable.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.EnvironmentVariables.Add(entity);
            variables.Add(entity);
            variableByKey[entity.Key] = entity;
        }

        foreach (var variable in NormalizeVariables(variableDiff.Updates.Select(entry => entry.Model).ToList()))
        {
            if (!variableByKey.TryGetValue(variable.Key, out var existing))
                continue;

            existing.Value = VariableSubstitution.Substitute(variable.Value, deploymentVariables, env);
            existing.Description = variable.Description;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    private void ApplySecrets(
        Guid workspaceId,
        IReadOnlyDictionary<string, string>? deploymentVariables,
        DiffResult<SecretModel> secretDiff,
        List<WorkspaceSecret> secrets,
        Dictionary<string, WorkspaceSecret> secretByKey,
        IReadOnlyDictionary<string, string>? env,
        IReadOnlyDictionary<string, string>? secretEncryptedValuesOverride)
    {
        foreach (var secret in NormalizeSecrets(secretDiff.Creations.Select(entry => entry.Model).ToList()))
        {
            string encryptedValue;
            if (secretEncryptedValuesOverride is not null && secretEncryptedValuesOverride.TryGetValue(secret.Key, out var preEncrypted))
            {
                encryptedValue = preEncrypted;
            }
            else
            {
                var rawValue = secret.Value ?? throw new InvalidOperationException(
                    $"Secret '{secret.Key}' must provide a value when it does not already exist.");
                encryptedValue = _secretProtector.Protect(VariableSubstitution.Substitute(rawValue, deploymentVariables, env));
            }

            var entity = new WorkspaceSecret
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                Key = secret.Key,
                Description = secret.Description,
                EncryptedValue = encryptedValue,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Secrets.Add(entity);
            secrets.Add(entity);
            secretByKey[entity.Key] = entity;
        }

        foreach (var secret in NormalizeSecrets(secretDiff.Updates.Select(entry => entry.Model).ToList()))
        {
            if (!secretByKey.TryGetValue(secret.Key, out var existing))
                continue;

            existing.Description = secret.Description;
            if (secretEncryptedValuesOverride is not null && secretEncryptedValuesOverride.TryGetValue(secret.Key, out var preEncrypted))
            {
                existing.EncryptedValue = preEncrypted;
            }
            else if (secret.Value is not null)
            {
                existing.EncryptedValue = _secretProtector.Protect(
                    VariableSubstitution.Substitute(secret.Value, deploymentVariables, env));
            }
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    private void ApplyWheelPackages(
        Guid workspaceId,
        Bundle bundle,
        DiffResult<WheelPackageModel> wheelDiff,
        List<WheelPackage> wheelPackages,
        Dictionary<string, WheelPackage> wheelByName)
    {
        foreach (var wheel in NormalizeWheelPackages(wheelDiff.Creations.Select(entry => entry.Model).ToList()))
        {
            var data = bundle.Files[wheel.BundleFilePath!];
            var entity = new WheelPackage
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                Name = wheel.Name,
                FileName = wheel.FileName,
                Data = data,
                Size = data.LongLength,
                UploadedAt = DateTime.UtcNow,
            };
            db.WheelPackages.Add(entity);
            wheelPackages.Add(entity);
            wheelByName[entity.Name] = entity;
        }

        foreach (var wheel in NormalizeWheelPackages(wheelDiff.Updates.Select(entry => entry.Model).ToList()))
        {
            if (!wheelByName.TryGetValue(wheel.Name, out var existing))
                continue;

            var data = bundle.Files[wheel.BundleFilePath!];
            existing.FileName = wheel.FileName;
            existing.Data = data;
            existing.Size = data.LongLength;
            existing.UploadedAt = DateTime.UtcNow;
        }
    }

    private void ApplyNotebook(
        Guid workspaceId,
        Bundle bundle,
        NotebookModel notebook,
        Dictionary<string, Folder> folderByPath,
        Dictionary<string, Notebook> notebookByPath,
        List<WorkspaceItem> items,
        bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(notebook.SourceFile) || !bundle.Files.TryGetValue(notebook.SourceFile, out var bytes))
            throw new InvalidOperationException($"Notebook '{notebook.Path}' is missing source content '{notebook.SourceFile}'.");

        if (isCreate || !notebookByPath.TryGetValue(notebook.Path, out var existing))
        {
            existing = new Notebook
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                Title = string.Empty,
                Content = string.Empty,
                CreatedAt = DateTime.UtcNow,
            };
            db.WorkspaceItems.Add(existing);
            items.Add(existing);
            notebookByPath[notebook.Path] = existing;
        }

        existing.Title = GetItemTitle(notebook.Path);
        existing.FolderId = ResolveFolderId(notebook.Path, folderByPath);
        existing.Content = Encoding.UTF8.GetString(bytes);
        existing.CatalogNames = NormalizeNameList(notebook.Catalogs);
        existing.UpdatedAt = DateTime.UtcNow;
    }

    private void ApplyQuery(
        Guid workspaceId,
        Bundle bundle,
        QueryModel query,
        Dictionary<string, Folder> folderByPath,
        Dictionary<string, Query> queryByPath,
        List<WorkspaceItem> items,
        bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(query.SourceFile) || !bundle.Files.TryGetValue(query.SourceFile, out var bytes))
            throw new InvalidOperationException($"Query '{query.Path}' is missing source content '{query.SourceFile}'.");

        if (isCreate || !queryByPath.TryGetValue(query.Path, out var existing))
        {
            existing = new Query
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                Title = string.Empty,
                Content = string.Empty,
                CreatedAt = DateTime.UtcNow,
            };
            db.WorkspaceItems.Add(existing);
            items.Add(existing);
            queryByPath[query.Path] = existing;
        }

        existing.Title = GetItemTitle(query.Path);
        existing.FolderId = ResolveFolderId(query.Path, folderByPath);
        existing.Content = Encoding.UTF8.GetString(bytes);
        existing.CatalogNames = NormalizeNameList(query.Catalogs);
        existing.UpdatedAt = DateTime.UtcNow;
    }

    private Folder EnsureFolderExists(
        Guid workspaceId,
        string folderPath,
        List<Folder> folders,
        Dictionary<string, Folder> folderByPath)
    {
        var normalizedPath = DeploymentPathHelpers.NormalizePath(folderPath);
        if (string.IsNullOrEmpty(normalizedPath))
            throw new InvalidOperationException("Folder path cannot be empty.");

        if (folderByPath.TryGetValue(normalizedPath, out var existing))
            return existing;

        Guid? parentId = null;
        var currentPath = string.Empty;
        Folder? current = null;

        foreach (var segment in normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";
            if (folderByPath.TryGetValue(currentPath, out current))
            {
                parentId = current.Id;
                continue;
            }

            current = new Folder
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                Name = segment,
                ParentId = parentId,
                CreatedAt = DateTime.UtcNow,
            };
            db.Folders.Add(current);
            folders.Add(current);
            folderByPath[currentPath] = current;
            parentId = current.Id;
        }

        return current!;
    }

    private static Dictionary<string, Folder> BuildFolderLookup(List<Folder> folders)
    {
        var folderPaths = DeploymentPathHelpers.BuildFolderPathMap(folders);
        return folders.ToDictionary(folder => folderPaths[folder.Id], StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, Notebook> BuildNotebookLookup(List<WorkspaceItem> items, List<Folder> folders)
    {
        var folderPaths = DeploymentPathHelpers.BuildFolderPathMap(folders);
        return items
            .OfType<Notebook>()
            .ToDictionary(notebook => DeploymentPathHelpers.GetItemPath(notebook, folderPaths), StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, Query> BuildQueryLookup(List<WorkspaceItem> items, List<Folder> folders)
    {
        var folderPaths = DeploymentPathHelpers.BuildFolderPathMap(folders);
        return items
            .OfType<Query>()
            .ToDictionary(query => DeploymentPathHelpers.GetItemPath(query, folderPaths), StringComparer.OrdinalIgnoreCase);
    }

    private static Guid? ResolveFolderId(string itemPath, Dictionary<string, Folder> folderByPath)
    {
        var normalized = DeploymentPathHelpers.NormalizePath(itemPath);
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash < 0)
            return null;

        var folderPath = normalized[..lastSlash];
        return folderByPath.TryGetValue(folderPath, out var folder)
            ? folder.Id
            : throw new InvalidOperationException($"Folder '{folderPath}' does not exist for item '{itemPath}'.");
    }

    private static string GetItemTitle(string itemPath)
    {
        var normalized = DeploymentPathHelpers.NormalizePath(itemPath);
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? normalized : normalized[(lastSlash + 1)..];
    }

    private static List<Guid>? ResolveIds<T>(
        List<string>? names,
        Dictionary<string, T> entitiesByName,
        string resourceType) where T : class
    {
        if (names is null || names.Count == 0)
            return null;

        var ids = new List<Guid>();
        foreach (var name in NormalizeNameList(names) ?? [])
        {
            if (!entitiesByName.TryGetValue(name, out var entity))
                throw new InvalidOperationException($"Referenced {resourceType} '{name}' was not found.");

            var idProperty = entity.GetType().GetProperty("Id")
                ?? throw new InvalidOperationException($"Entity type '{entity.GetType().Name}' does not expose an Id property.");
            ids.Add((Guid)(idProperty.GetValue(entity) ?? Guid.Empty));
        }

        return ids;
    }

    private NodePoolConfig CreateNodePool(
        Guid workspaceId,
        NodePoolModel model,
        Dictionary<string, WheelPackage> wheelByName,
        Dictionary<string, EnvironmentVariable> variableByKey,
        Dictionary<string, WorkspaceSecret> secretByKey)
    {
        var isInteractive = string.Equals(model.Type, "interactive", StringComparison.OrdinalIgnoreCase);
        var commonName = model.Name;
        var commonVmSize = model.VmSize;
        var commonDescription = model.Description;
        var commonKernelRequirements = model.KernelRequirements;
        var commonNodeIdleTimeout = ParseNullableTimeSpan(model.NodeIdleTimeout, $"node pool '{model.Name}' node_idle_timeout");
        var commonWheelIds = ResolveIds(model.WheelPackages, wheelByName, "wheel package");
        var commonVariableIds = ResolveIds(model.EnvironmentVariables, variableByKey, "environment variable");
        var commonSecretIds = ResolveIds(model.Secrets, secretByKey, "secret");
        var now = DateTime.UtcNow;

        return isInteractive
            ? new InteractiveNodePoolConfig
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                Name = commonName,
                VmSize = commonVmSize,
                Description = commonDescription,
                KernelRequirements = commonKernelRequirements,
                NodeIdleTimeout = commonNodeIdleTimeout,
                WheelPackageIds = commonWheelIds,
                EnvironmentVariableIds = commonVariableIds,
                SecretIds = commonSecretIds,
                CreatedAt = now,
                UpdatedAt = now,
            }
            : new JobNodePoolConfig
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                Name = commonName,
                VmSize = commonVmSize,
                Description = commonDescription,
                KernelRequirements = commonKernelRequirements,
                NodeIdleTimeout = commonNodeIdleTimeout,
                WheelPackageIds = commonWheelIds,
                EnvironmentVariableIds = commonVariableIds,
                SecretIds = commonSecretIds,
                WarmNodes = model.WarmNodes ?? 0,
                MaxNodes = model.MaxNodes,
                NodeAcquireTimeout = ParseNullableTimeSpan(model.NodeAcquireTimeout, $"node pool '{model.Name}' node_acquire_timeout"),
                CreatedAt = now,
                UpdatedAt = now,
            };
    }

    private static TimeSpan? ParseNullableTimeSpan(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!TimeSpan.TryParse(value, out var parsed))
            throw new InvalidOperationException($"Invalid duration '{value}' for {field}.");

        return parsed;
    }

    private static void ValidateWorkspaceBundle(Bundle bundle)
    {
        if (!bundle.Manifest.Scope.Equals("workspace", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Expected a workspace bundle but received '{bundle.Manifest.Scope}'.");
    }

    private static void ValidateBundleFiles(Bundle bundle)
    {
        foreach (var notebook in bundle.Notebooks)
        {
            if (string.IsNullOrWhiteSpace(notebook.SourceFile) || !bundle.Files.ContainsKey(notebook.SourceFile))
                throw new InvalidOperationException($"Notebook '{notebook.Path}' is missing source file '{notebook.SourceFile}'.");
        }

        foreach (var query in bundle.Queries)
        {
            if (string.IsNullOrWhiteSpace(query.SourceFile) || !bundle.Files.ContainsKey(query.SourceFile))
                throw new InvalidOperationException($"Query '{query.Path}' is missing source file '{query.SourceFile}'.");
        }

        foreach (var wheel in bundle.WheelPackages)
        {
            if (string.IsNullOrWhiteSpace(wheel.BundleFilePath) || !bundle.Files.ContainsKey(wheel.BundleFilePath))
                throw new InvalidOperationException($"Wheel package '{wheel.Name}' is missing bundle file '{wheel.BundleFilePath}'.");
        }
    }

    private static void ValidateSecretCreates(DiffResult<SecretModel> secretDiff)
    {
        var missingValueSecret = secretDiff.Creations
            .Select(entry => entry.Model)
            .FirstOrDefault(secret => string.IsNullOrWhiteSpace(secret.Value));
        if (missingValueSecret is not null)
        {
            throw new InvalidOperationException(
                $"Secret '{missingValueSecret.Key}' must provide a value when it does not already exist.");
        }
    }

    private static List<string>? NormalizeNameList(List<string>? values)
    {
        var normalized = (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return normalized.Count == 0 ? null : normalized;
    }

    private static List<FolderModel> NormalizeFolders(List<FolderModel> models) =>
        models
            .Select(model => new FolderModel { Path = DeploymentPathHelpers.NormalizePath(model.Path) })
            .OrderBy(model => model.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<NotebookModel> NormalizeNotebooks(List<NotebookModel> models) =>
        models
            .Select(model => new NotebookModel
            {
                Path = DeploymentPathHelpers.NormalizePath(model.Path),
                SourceFile = model.SourceFile,
                Catalogs = NormalizeNameList(model.Catalogs),
            })
            .OrderBy(model => model.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<QueryModel> NormalizeQueries(List<QueryModel> models) =>
        models
            .Select(model => new QueryModel
            {
                Path = DeploymentPathHelpers.NormalizePath(model.Path),
                SourceFile = model.SourceFile,
                Catalogs = NormalizeNameList(model.Catalogs),
            })
            .OrderBy(model => model.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<NodePoolModel> NormalizeNodePools(List<NodePoolModel> models) =>
        models
            .Select(model => new NodePoolModel
            {
                Name = model.Name.Trim(),
                Type = string.IsNullOrWhiteSpace(model.Type) ? "job" : model.Type.Trim(),
                VmSize = model.VmSize,
                KernelRequirements = model.KernelRequirements,
                Description = model.Description,
                WarmNodes = model.WarmNodes,
                MaxNodes = model.MaxNodes,
                NodeAcquireTimeout = model.NodeAcquireTimeout,
                NodeIdleTimeout = model.NodeIdleTimeout,
                WheelPackages = NormalizeNameList(model.WheelPackages),
                EnvironmentVariables = NormalizeNameList(model.EnvironmentVariables),
                Secrets = NormalizeNameList(model.Secrets),
            })
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<EnvironmentVariableModel> NormalizeVariables(List<EnvironmentVariableModel> models) =>
        models
            .Select(model => new EnvironmentVariableModel
            {
                Key = model.Key.Trim(),
                Value = model.Value,
                Description = model.Description,
            })
            .OrderBy(model => model.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<SecretModel> NormalizeSecrets(List<SecretModel> models) =>
        models
            .Select(model => new SecretModel
            {
                Key = model.Key.Trim(),
                Description = model.Description,
                Value = model.Value,
            })
            .OrderBy(model => model.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<WheelPackageModel> NormalizeWheelPackages(List<WheelPackageModel> models) =>
        models
            .Select(model => new WheelPackageModel
            {
                Name = model.Name.Trim(),
                FileName = model.FileName,
                BundleFilePath = model.BundleFilePath,
            })
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
