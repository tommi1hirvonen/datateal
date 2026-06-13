using System.Text;
using Datateal.Core.Catalogs;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Application.Mediator.Queries;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Ai;
using Microsoft.Extensions.AI;

namespace Datateal.Ui.Server.Application.Ai;

/// <summary>
/// Assembles the full AI conversation context from the request:
/// system prompt (Datateal instructions + catalog schemas + package list) + message history.
/// </summary>
public class ContextAssembler(
    IMediator mediator,
    IWheelPackageRepository wheelPackageRepository)
    : IContextAssembler
{
    public async Task<IReadOnlyList<ChatMessage>> AssembleAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var catalogSchemaText = await BuildCatalogSchemaTextAsync(request.CatalogIds, cancellationToken);
        var packageNames = await GetPackageNamesAsync(request.WorkspaceId, cancellationToken);
        var systemPrompt = SystemPromptBuilder.Build(packageNames, catalogSchemaText);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
        };

        // Add notebook or query content as an initial assistant-visible context message
        var contextPreamble = BuildContextPreamble(request);
        if (contextPreamble is not null)
            messages.Add(new ChatMessage(ChatRole.User, contextPreamble));

        // Map conversation history (skip the preamble if this is the first turn)
        foreach (var msg in request.Messages)
        {
            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? ChatRole.Assistant
                : ChatRole.User;
            messages.Add(new ChatMessage(role, msg.Content));
        }

        return messages;
    }

    private async Task<string?> BuildCatalogSchemaTextAsync(
        IReadOnlyList<Guid> catalogIds,
        CancellationToken ct)
    {
        if (catalogIds.Count == 0)
            return null;

        var sb = new StringBuilder();
        foreach (var catalogId in catalogIds)
        {
            try
            {
                var metadata = await mediator.SendAsync(new GetCatalogMetadataRequest(catalogId), ct);
                if (metadata is null)
                    continue;

                sb.AppendLine($"### Catalog name: {metadata.Name}");
                foreach (var schema in metadata.Schemas)
                {
                    sb.AppendLine($"#### Schema: {schema.Name}");
                    foreach (var table in schema.Tables)
                    {
                        var tableComment = string.IsNullOrEmpty(table.Comment)
                            ? string.Empty
                            : $" -- {table.Comment}";
                        sb.AppendLine($"- **{table.Type}** `{metadata.Name}.{schema.Name}.{table.Name}`{tableComment}");
                        foreach (var col in table.Columns.OrderBy(c => c.OrdinalPosition))
                        {
                            var nullable = col.IsNullable ? "?" : string.Empty;
                            var colComment = string.IsNullOrEmpty(col.Comment) ? string.Empty : $" -- {col.Comment}";
                            sb.AppendLine($"  - `{col.Name}` {col.DataType}{nullable}{colComment}");
                        }
                    }
                }

                sb.AppendLine();
            }
            catch
            {
                // If metadata fetch fails for one catalog, skip it and continue
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private async Task<IEnumerable<string>> GetPackageNamesAsync(Guid? workspaceId, CancellationToken ct)
    {
        if (workspaceId is null)
            return [];

        var packages = await wheelPackageRepository.GetAllAsync(workspaceId.Value, ct);
        return packages.Select(p => p.Name);
    }

    private static string? BuildContextPreamble(AiChatRequest request)
    {
        // Always prepend context so the AI has the latest notebook/query content,
        // even on subsequent turns as edits are made during the conversation.
        return request.ContextType switch
        {
            AiContextType.NotebookCell => BuildNotebookCellPreamble(request),
            AiContextType.Notebook => BuildNotebookPreamble(request),
            AiContextType.Query => BuildQueryPreamble(request),
            _ => null,
        };
    }

    private static string? BuildNotebookCellPreamble(AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NotebookJson))
            return null;

        return $"""
            I'm working in a Datateal notebook. Here is the full notebook content for context:

            ```json
            {request.NotebookJson}
            ```

            I'm currently focused on cell index {request.FocusedCellIndex} (0-based).
            """;
    }

    private static string? BuildNotebookPreamble(AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NotebookJson))
            return null;

        if (request.Mode == AiMode.Edit)
        {
            return $"""
                I'm working in a Datateal notebook and need you to act as an agent making bulk edits.
                Here is the current notebook content (cells are 0-indexed):

                ```json
                {request.NotebookJson}
                ```

                For EACH change you want to make, call the appropriate tool:
                - `propose_cell_edit(cellIndex, newContent, explanation)` — replace the full content of an existing cell
                - `propose_cell_insert(afterCellIndex, language, content, explanation)` — insert a new cell; use afterCellIndex=-1 to insert before the first cell; language must be "python", "sql", or "markdown"; ALL indices refer to the ORIGINAL notebook before any of your proposed changes
                - `propose_cell_remove(cellIndex, explanation)` — remove an existing cell

                Do NOT describe changes in text — use the tools for every change.
                After all tool calls, write a short summary of what you changed and why.
                """;
        }

        return $"""
            I'm working in a Datateal notebook and would like help with the entire notebook.
            Here is the current notebook content:

            ```json
            {request.NotebookJson}
            ```
            """;
    }

    private static string? BuildQueryPreamble(AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QueryContent))
            return null;

        return $"""
            I'm working on a DuckDB SQL query. Here is the current query:

            ```sql
            {request.QueryContent}
            ```
            """;
    }
}
