namespace Datateal.Ui.Shared.Ai;

/// <summary>The AI assistant interaction mode.</summary>
public enum AiMode
{
    /// <summary>Conversational mode — AI answers questions and suggests code inline.</summary>
    Chat,
    /// <summary>Edit mode — AI proposes bulk cell changes via tool calls; user reviews before applying.</summary>
    Edit,
}

/// <summary>Context type that determines what content the AI is operating on.</summary>
public enum AiContextType
{
    /// <summary>Working on an individual notebook cell; full notebook content is included for context.</summary>
    NotebookCell,
    /// <summary>Working on the notebook as a whole.</summary>
    Notebook,
    /// <summary>Working on a SQL query file.</summary>
    Query,
}

/// <summary>A single message in the chat history.</summary>
public record AiChatMessage(string Role, string Content);

/// <summary>The type of operation the AI agent is proposing for a notebook cell.</summary>
public enum CellProposalOperation
{
    /// <summary>Replace the entire content of an existing cell.</summary>
    Edit,
    /// <summary>Insert a new cell after the specified index (-1 = before the first cell).</summary>
    Insert,
    /// <summary>Remove the cell at the specified index.</summary>
    Remove,
}

/// <summary>
/// A proposed change to a notebook cell produced by the AI agent.
/// </summary>
public record CellProposal(
    /// <summary>The type of operation.</summary>
    CellProposalOperation Operation,
    /// <summary>
    /// 0-based cell index.
    /// For Edit/Remove: the target cell.
    /// For Insert: the new cell is placed AFTER this index; use -1 to insert before the first cell.
    /// </summary>
    int CellIndex,
    /// <summary>Full content for the cell. Used by Edit and Insert; null for Remove.</summary>
    string? NewContent,
    /// <summary>Human-readable explanation of the change.</summary>
    string Explanation,
    /// <summary>Cell language for Insert operations: "python", "sql", or "markdown". Null for Edit/Remove.</summary>
    string? Language = null);

/// <summary>
/// Request to start an AI chat completion stream.
/// Sent from the WASM client to the server SignalR hub.
/// </summary>
public record AiChatRequest(
    AiProviderType Provider,
    /// <summary>API key for the selected provider. Never persisted server-side.</summary>
    string ApiKey,
    /// <summary>Provider endpoint URL (e.g. Azure OpenAI resource endpoint).</summary>
    string Endpoint,
    /// <summary>Model / deployment name (e.g. "gpt-4o").</summary>
    string Model,
    AiContextType ContextType,
    /// <summary>The conversation history so far (user + assistant turns).</summary>
    IReadOnlyList<AiChatMessage> Messages,
    /// <summary>
    /// Full notebook content as JSON (ipynb format), if context involves a notebook.
    /// Null for Query context.
    /// </summary>
    string? NotebookJson,
    /// <summary>Index of the cell the user is focused on (-1 for notebook-level context).</summary>
    int FocusedCellIndex,
    /// <summary>
    /// Current SQL query content, for Query context.
    /// Null for Notebook/NotebookCell context.
    /// </summary>
    string? QueryContent,
    /// <summary>The active workspace ID, when the AI context is workspace-scoped.</summary>
    Guid? WorkspaceId,
    /// <summary>IDs of catalogs attached to the current workspace item.</summary>
    IReadOnlyList<Guid> CatalogIds,
    /// <summary>The interaction mode. Defaults to Chat.</summary>
    AiMode Mode = AiMode.Chat
);
