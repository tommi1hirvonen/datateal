using System.Runtime.CompilerServices;
using Datateal.Core.RuntimePackages;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;

[assembly: InternalsVisibleTo("Datateal.Core.Tests")]

namespace Datateal.Ui.Server.Infrastructure.Deployment;

/// <summary>
/// Validates all cross-references and required fields within a workspace-scope bundle
/// against the bundle itself and the existing workspace state. Collects every error before
/// throwing so a single plan run reveals the full list of problems.
/// </summary>
internal static class WorkspaceBundleValidator
{
    private static readonly HashSet<string> ValidNodePoolTypes =
        new(["interactive", "job"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ValidTaskTypes =
        new(["notebook", "sql_query", "sql", "sub_job"], StringComparer.OrdinalIgnoreCase);

    public static void Validate(
        Bundle bundle,
        IReadOnlyDictionary<string, string>? variables = null,
        IReadOnlyDictionary<string, string>? env = null)
    {
        var errors = new List<string>();

        // ── Required fields ────────────────────────────────────────────────────

        foreach (var pool in bundle.NodePools)
        {
            if (string.IsNullOrWhiteSpace(pool.Name))
                errors.Add("A node pool entry is missing a required 'name'.");
            if (string.IsNullOrWhiteSpace(pool.VmSize))
                errors.Add($"Node pool '{pool.Name}' is missing required field 'vm_size'.");
            if (!string.IsNullOrWhiteSpace(pool.Type) && !ValidNodePoolTypes.Contains(pool.Type))
                errors.Add($"Node pool '{pool.Name}' has unknown type '{pool.Type}'. Expected 'interactive' or 'job'.");
        }

        foreach (var variable in bundle.EnvironmentVariables)
        {
            if (string.IsNullOrWhiteSpace(variable.Key))
                errors.Add("An environment variable entry is missing a required 'key'.");
        }

        foreach (var secret in bundle.Secrets)
        {
            if (string.IsNullOrWhiteSpace(secret.Key))
                errors.Add("A secret entry is missing a required 'key'.");
        }

        foreach (var wheel in bundle.WheelPackages)
        {
            if (string.IsNullOrWhiteSpace(wheel.Name))
                errors.Add("A wheel package entry is missing a required 'name'.");

            // Bundle file presence is validated separately (missing source files are a distinct,
            // earlier error); only check size when the file is actually present.
            if (!string.IsNullOrWhiteSpace(wheel.BundleFilePath)
                && bundle.Files.TryGetValue(wheel.BundleFilePath, out var wheelBytes)
                && wheelBytes.LongLength > WheelPackageLimits.MaxFileSizeBytes)
            {
                errors.Add(
                    $"Wheel package '{wheel.Name}' exceeds the maximum size of " +
                    $"{WheelPackageLimits.MaxFileSizeBytes / 1024 / 1024} MB.");
            }
        }

        foreach (var job in bundle.Jobs)
        {
            if (string.IsNullOrWhiteSpace(job.Name))
                errors.Add("A job entry is missing a required 'name'.");

            var seenTaskNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var task in job.Tasks)
            {
                if (string.IsNullOrWhiteSpace(task.Name))
                    errors.Add($"Job '{job.Name}' has a task missing a required 'name'.");

                if (string.IsNullOrWhiteSpace(task.Type))
                {
                    errors.Add($"Job '{job.Name}' task '{task.Name}' is missing a required 'type'.");
                    continue; // type-specific checks below need a valid type
                }

                if (!ValidTaskTypes.Contains(task.Type))
                    errors.Add($"Job '{job.Name}' task '{task.Name}' has unknown type '{task.Type}'.");

                if (!string.IsNullOrWhiteSpace(task.Name) && !seenTaskNames.Add(task.Name))
                    errors.Add($"Job '{job.Name}' has duplicate task name '{task.Name}'.");

                var taskType = NormalizeTaskType(task.Type);
                switch (taskType)
                {
                    case "notebook":
                        if (string.IsNullOrWhiteSpace(task.NotebookPath))
                            errors.Add($"Job '{job.Name}' task '{task.Name}' (notebook) is missing required field 'notebook_path'.");
                        if (string.IsNullOrWhiteSpace(task.NodePoolRef))
                            errors.Add($"Job '{job.Name}' task '{task.Name}' (notebook) is missing required field 'node_pool_ref'.");
                        break;
                    case "sql_query":
                        if (string.IsNullOrWhiteSpace(task.QueryPath))
                            errors.Add($"Job '{job.Name}' task '{task.Name}' (sql_query) is missing required field 'query_path'.");
                        if (string.IsNullOrWhiteSpace(task.NodePoolRef))
                            errors.Add($"Job '{job.Name}' task '{task.Name}' (sql_query) is missing required field 'node_pool_ref'.");
                        break;
                    case "sub_job":
                        if (string.IsNullOrWhiteSpace(task.JobName))
                            errors.Add($"Job '{job.Name}' task '{task.Name}' (sub_job) is missing required field 'job_name'.");
                        break;
                }
            }
        }

        // ── Cross-reference checks ─────────────────────────────────────────────

        // Build available sets from target bundle state (since workspace scope reconciles all resources with allowDeletes: true)
        var availableNotebooks = BuildSet(bundle.Notebooks.Select(n => n.Path.Trim('/')));
        var availableQueries = BuildSet(bundle.Queries.Select(q => q.Path.Trim('/')));
        var availableNodePools = BuildSet(bundle.NodePools.Select(p => p.Name));
        var availableVariables = BuildSet(bundle.EnvironmentVariables.Select(v => v.Key));
        var availableSecrets = BuildSet(bundle.Secrets.Select(s => s.Key));
        var availableWheels = BuildSet(bundle.WheelPackages.Select(w => w.Name));
        var availableJobs = BuildSet(bundle.Jobs.Select(j => j.Name));

        foreach (var pool in bundle.NodePools)
        {
            foreach (var name in pool.WheelPackages ?? [])
                if (!availableWheels.Contains(name))
                    errors.Add($"Node pool '{pool.Name}' references wheel package '{name}' which does not exist in the workspace or bundle.");

            foreach (var key in pool.EnvironmentVariables ?? [])
                if (!availableVariables.Contains(key))
                    errors.Add($"Node pool '{pool.Name}' references environment variable '{key}' which does not exist in the workspace or bundle.");

            foreach (var key in pool.Secrets ?? [])
                if (!availableSecrets.Contains(key))
                    errors.Add($"Node pool '{pool.Name}' references secret '{key}' which does not exist in the workspace or bundle.");
        }

        foreach (var job in bundle.Jobs)
        {
            var taskNames = job.Tasks.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var task in job.Tasks)
            {
                if (!string.IsNullOrWhiteSpace(task.NodePoolRef) && !availableNodePools.Contains(task.NodePoolRef))
                    errors.Add($"Job '{job.Name}' task '{task.Name}' references node pool '{task.NodePoolRef}' which does not exist in the workspace or bundle.");

                var taskType = NormalizeTaskType(task.Type ?? "");
                switch (taskType)
                {
                    case "notebook":
                        var notebookPath = task.NotebookPath?.Trim('/');
                        if (!string.IsNullOrWhiteSpace(notebookPath) && !availableNotebooks.Contains(notebookPath))
                            errors.Add($"Job '{job.Name}' task '{task.Name}' references notebook '{task.NotebookPath}' which does not exist in the workspace or bundle.");
                        break;
                    case "sql_query":
                        var queryPath = task.QueryPath?.Trim('/');
                        if (!string.IsNullOrWhiteSpace(queryPath) && !availableQueries.Contains(queryPath))
                            errors.Add($"Job '{job.Name}' task '{task.Name}' references query '{task.QueryPath}' which does not exist in the workspace or bundle.");
                        break;
                    case "sub_job":
                        if (!string.IsNullOrWhiteSpace(task.JobName) && !availableJobs.Contains(task.JobName))
                            errors.Add($"Job '{job.Name}' task '{task.Name}' references sub-job '{task.JobName}' which does not exist in the workspace or bundle.");
                        break;
                }

                foreach (var dep in task.Dependencies)
                    if (!string.IsNullOrWhiteSpace(dep.Task) && !taskNames.Contains(dep.Task))
                        errors.Add($"Job '{job.Name}' task '{task.Name}' has a dependency on task '{dep.Task}' which does not exist in the same job.");
            }
        }

        // ── Variable/env substitution checks ───────────────────────────────────
        // Resolved here (purely for validation — results are discarded) so an unresolved
        // ${var.X}/${env.X} token is reported during Plan, not only when Apply actually
        // substitutes the value.

        foreach (var variable in bundle.EnvironmentVariables)
            ValidateSubstitutable(variable.Value, variables, env, errors);

        foreach (var secret in bundle.Secrets)
            if (secret.Value is not null)
                ValidateSubstitutable(secret.Value, variables, env, errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Bundle validation failed with {errors.Count} error(s):\n" +
                string.Join("\n", errors.Select((e, i) => $"  {i + 1}. {e}")));
    }

    /// <summary>
    /// Attempts to resolve every <c>${var.X}</c>/<c>${env.X}</c> token in <paramref name="value"/>,
    /// discarding the result — only used to surface <see cref="DeploymentVariableException"/>
    /// messages as ordinary validation errors instead of letting them propagate individually.
    /// </summary>
    private static void ValidateSubstitutable(
        string? value,
        IReadOnlyDictionary<string, string>? variables,
        IReadOnlyDictionary<string, string>? env,
        List<string> errors)
    {
        if (string.IsNullOrEmpty(value)) return;

        try
        {
            VariableSubstitution.Substitute(value, variables, env);
        }
        catch (DeploymentVariableException ex)
        {
            errors.Add(ex.Message);
        }
    }

    private static HashSet<string> BuildSet(IEnumerable<string> bundleValues) =>
        new(bundleValues.Where(v => !string.IsNullOrWhiteSpace(v)),
            StringComparer.OrdinalIgnoreCase);

    private static string NormalizeTaskType(string type) =>
        type.Trim().ToLowerInvariant() switch
        {
            "sql" => "sql_query",
            var t => t,
        };
}
