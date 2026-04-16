namespace DuckHouse.Ui.Client.Notebook;

/// <param name="Label">Display name (e.g., "subfolder/", "my_notebook").</param>
/// <param name="Kind">"folder", "notebook", or "query".</param>
/// <param name="InsertText">Text to insert on completion accept (folders include trailing /).</param>
public record RunMagicCompletionItem(string Label, string Kind, string InsertText);
