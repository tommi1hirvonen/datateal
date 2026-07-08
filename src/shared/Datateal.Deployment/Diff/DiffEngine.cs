namespace Datateal.Deployment.Diff;

/// <summary>
/// Pluggable resource mapper that translates between a domain entity and its
/// deployment POCO model.
/// </summary>
/// <typeparam name="TModel">The deployment POCO model type.</typeparam>
public interface IResourceMapper<TModel>
{
    /// <summary>Returns the resource type label (e.g. "notebook") for change-set output.</summary>
    string ResourceType { get; }

    /// <summary>Returns the natural key for a model (used for diff matching and change-set names).</summary>
    string NaturalKey(TModel model);

    /// <summary>Returns true if the desired model and the current model represent the same state.</summary>
    bool AreEqual(TModel desired, TModel current);
}

/// <summary>
/// Generic diff engine. Given a desired list and a current list of models (keyed by
/// natural key), produces a <see cref="DiffResult{TModel}"/> describing the changes.
/// </summary>
public static class DiffEngine
{
    /// <summary>
    /// Computes the diff between <paramref name="desired"/> and <paramref name="current"/>.
    /// </summary>
    /// <param name="mapper">Mapper that provides natural keys and equality comparison.</param>
    /// <param name="desired">The list of models from the bundle (desired state).</param>
    /// <param name="current">The list of models from the live environment (current state).</param>
    /// <param name="allowDeletes">
    ///   When true (workspace scope), resources in <paramref name="current"/> that are absent
    ///   from <paramref name="desired"/> are tagged <see cref="ChangeType.Delete"/>.
    ///   When false (admin scope), they are omitted from the result.
    /// </param>
    public static DiffResult<TModel> Diff<TModel>(
        IResourceMapper<TModel> mapper,
        IEnumerable<TModel> desired,
        IEnumerable<TModel> current,
        bool allowDeletes)
    {
        var currentByKey = current.ToDictionary(mapper.NaturalKey, StringComparer.OrdinalIgnoreCase);
        var desiredList = desired.ToList();

        var changes = new List<(TModel? Model, ResourceChange Change)>();

        foreach (var desiredModel in desiredList)
        {
            var key = mapper.NaturalKey(desiredModel);
            if (currentByKey.TryGetValue(key, out var existing))
            {
                var changeType = mapper.AreEqual(desiredModel, existing)
                    ? ChangeType.NoChange
                    : ChangeType.Update;

                changes.Add((desiredModel, new ResourceChange
                {
                    ResourceType = mapper.ResourceType,
                    ResourceName = key,
                    ChangeType = changeType,
                }));
            }
            else
            {
                changes.Add((desiredModel, new ResourceChange
                {
                    ResourceType = mapper.ResourceType,
                    ResourceName = key,
                    ChangeType = ChangeType.Create,
                }));
            }
        }

        if (allowDeletes)
        {
            var desiredKeys = desiredList
                .Select(mapper.NaturalKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, existing) in currentByKey)
            {
                if (!desiredKeys.Contains(key))
                {
                    changes.Add((existing, new ResourceChange
                    {
                        ResourceType = mapper.ResourceType,
                        ResourceName = key,
                        ChangeType = ChangeType.Delete,
                    }));
                }
            }
        }

        return new DiffResult<TModel>(changes);
    }
}

/// <summary>Result of a <see cref="DiffEngine.Diff{TModel}"/> call.</summary>
public class DiffResult<TModel>
{
    private readonly List<(TModel? Model, ResourceChange Change)> _entries;

    internal DiffResult(List<(TModel? Model, ResourceChange Change)> entries)
    {
        _entries = entries;
    }

    public IReadOnlyList<ResourceChange> Changes => _entries.Select(e => e.Change).ToList();

    public IEnumerable<(TModel Model, ResourceChange Change)> Creations =>
        _entries
            .Where(e => e.Change.ChangeType == ChangeType.Create && e.Model is not null)
            .Select(e => (e.Model!, e.Change));

    public IEnumerable<(TModel Model, ResourceChange Change)> Updates =>
        _entries
            .Where(e => e.Change.ChangeType == ChangeType.Update && e.Model is not null)
            .Select(e => (e.Model!, e.Change));

    public IEnumerable<(TModel Model, ResourceChange Change)> Deletions =>
        _entries
            .Where(e => e.Change.ChangeType == ChangeType.Delete && e.Model is not null)
            .Select(e => (e.Model!, e.Change));
}
