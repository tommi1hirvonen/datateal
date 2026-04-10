using DuckHouse.Orchestrator.Core.Entities;

namespace DuckHouse.Orchestrator.Application.Validation;

public static class DagValidator
{
    /// <summary>
    /// Validates that the task dependency graph is a valid DAG (no cycles) and that
    /// all dependency references point to tasks within the same job.
    /// Uses Kahn's algorithm for topological sort.
    /// </summary>
    public static void Validate(IReadOnlyList<JobTask> tasks)
    {
        var taskIds = new HashSet<Guid>(tasks.Select(t => t.Id));

        // Validate all dependency references point to valid tasks
        foreach (var task in tasks)
        {
            foreach (var dep in task.Dependencies)
            {
                if (!taskIds.Contains(dep.DependsOnTaskId))
                    throw new InvalidOperationException(
                        $"Task '{task.Name}' has a dependency on task ID '{dep.DependsOnTaskId}' which does not exist in the job.");
            }
        }

        // Build adjacency list and in-degree map (Kahn's algorithm)
        var inDegree = new Dictionary<Guid, int>();
        var adjacency = new Dictionary<Guid, List<Guid>>();

        foreach (var task in tasks)
        {
            inDegree[task.Id] = 0;
            adjacency[task.Id] = [];
        }

        foreach (var task in tasks)
        {
            foreach (var dep in task.Dependencies)
            {
                // dep.DependsOnTaskId → task.Id (the depended-on task must complete first)
                adjacency[dep.DependsOnTaskId].Add(task.Id);
                inDegree[task.Id]++;
            }
        }

        // Seed queue with tasks that have no dependencies
        var queue = new Queue<Guid>();
        foreach (var (id, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(id);
        }

        var sortedCount = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sortedCount++;

            foreach (var neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        if (sortedCount != tasks.Count)
            throw new InvalidOperationException(
                "The task dependency graph contains a cycle. Please remove circular dependencies.");
    }
}
