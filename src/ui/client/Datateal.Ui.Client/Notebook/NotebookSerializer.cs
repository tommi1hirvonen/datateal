using System.Text;
using System.Text.Json;
using Datateal.Core.Kernels;

namespace Datateal.Ui.Client.Notebook;

public static class NotebookSerializer
{
    public static NotebookDocument Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var document = new NotebookDocument();

        if (!root.TryGetProperty("cells", out var cellsEl)) return document;

        foreach (var cellEl in cellsEl.EnumerateArray())
        {
            var cell = DeserializeCell(cellEl);
            if (cell is not null) document.Cells.Add(cell);
        }

        return document;
    }

    private static NotebookCell? DeserializeCell(JsonElement el)
    {
        if (!el.TryGetProperty("cell_type", out var typeEl)) return null;

        var cellType = typeEl.GetString() == "markdown"
            ? NotebookCellType.Markdown
            : NotebookCellType.Code;

        var cell = new NotebookCell { CellType = cellType };

        if (el.TryGetProperty("id", out var idEl))
            cell.Id = idEl.GetString() ?? cell.Id;

        cell.Source = ReadMultilineString(el, "source");

        if (cellType != NotebookCellType.Code) return cell;

        if (el.TryGetProperty("metadata", out var meta))
        {
            if (meta.TryGetProperty("language", out var langEl))
                cell.Language = langEl.GetString() == "sql" ? NotebookCellLanguage.Sql : NotebookCellLanguage.Python;

            if (meta.TryGetProperty("datateal", out var dh)
                && dh.TryGetProperty("duration_ms", out var durEl)
                && durEl.ValueKind != JsonValueKind.Null
                && durEl.TryGetDouble(out var dur))
            {
                cell.DurationMs = dur;
            }

            if (meta.TryGetProperty("tags", out var tagsEl))
            {
                var tags = tagsEl.EnumerateArray().Select(t => t.GetString()).ToList();
                // Skip injected-parameters cells — they are runtime artifacts, not part of the editable notebook
                if (tags.Contains("injected-parameters")) return null;
                cell.IsParameterCell = tags.Contains("parameters");
            }
        }

        if (el.TryGetProperty("execution_count", out var ecEl) && ecEl.ValueKind != JsonValueKind.Null)
            cell.ExecutionCount = ecEl.GetInt32();

        if (!el.TryGetProperty("outputs", out var outputsEl)) return cell;

        foreach (var outEl in outputsEl.EnumerateArray())
        {
            if (!outEl.TryGetProperty("output_type", out var outTypeEl)) continue;
            var outType = outTypeEl.GetString();

            if (outType == "error")
            {
                var ename = outEl.TryGetProperty("ename", out var en) ? en.GetString() ?? "" : "";
                var evalue = outEl.TryGetProperty("evalue", out var ev) ? ev.GetString() ?? "" : "";
                IReadOnlyList<string> traceback = outEl.TryGetProperty("traceback", out var tb)
                    ? tb.EnumerateArray().Select(t => t.GetString() ?? "").ToList()
                    : [];
                cell.Error = new ErrorInfo(ename, evalue, traceback);
            }
            else if (outType == "stream")
            {
                var name = outEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                var text = ReadMultilineString(outEl, "text");
                cell.Outputs.Add(new Output("stream", name, text, null, null));
            }
            else if (outType is "execute_result" or "display_data")
            {
                int? execCount = null;
                if (outEl.TryGetProperty("execution_count", out var oc) && oc.ValueKind != JsonValueKind.Null)
                    execCount = oc.GetInt32();

                var data = new Dictionary<string, object>();
                if (outEl.TryGetProperty("data", out var dataEl))
                {
                    foreach (var prop in dataEl.EnumerateObject())
                    {
                        data[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.Array  => string.Join("", prop.Value.EnumerateArray().Select(l => l.GetString() ?? "")),
                            JsonValueKind.Object => prop.Value.Clone(),
                            _                    => (object)(prop.Value.GetString() ?? ""),
                        };
                    }
                }

                cell.Outputs.Add(new Output(outType, null, null, data, execCount));
            }
        }

        return cell;
    }

    private static string ReadMultilineString(JsonElement el, string propertyName)
    {
        if (!el.TryGetProperty(propertyName, out var prop)) return "";
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Array  => string.Join("", prop.EnumerateArray().Select(l => l.GetString() ?? "")),
            _                    => "",
        };
    }

    public static string Serialize(NotebookDocument document)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteNumber("nbformat", 4);
        writer.WriteNumber("nbformat_minor", 5);

        writer.WriteStartObject("metadata");
        writer.WriteStartObject("kernelspec");
        writer.WriteString("display_name", "Datateal");
        writer.WriteString("language", "python");
        writer.WriteString("name", "datateal");
        writer.WriteEndObject();
        writer.WriteStartObject("datateal");
        writer.WriteString("version", "1");
        writer.WriteEndObject();
        writer.WriteEndObject(); // metadata

        writer.WriteStartArray("cells");
        foreach (var cell in document.Cells)
            WriteCell(writer, cell);
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteCell(Utf8JsonWriter writer, NotebookCell cell)
    {
        writer.WriteStartObject();
        writer.WriteString("id", cell.Id);
        writer.WriteString("cell_type", cell.CellType == NotebookCellType.Markdown ? "markdown" : "code");
        writer.WriteString("source", cell.Source);

        if (cell.CellType == NotebookCellType.Markdown)
        {
            writer.WriteStartObject("metadata");
            writer.WriteEndObject();
        }
        else
        {
            if (cell.ExecutionCount.HasValue)
                writer.WriteNumber("execution_count", cell.ExecutionCount.Value);
            else
                writer.WriteNull("execution_count");

            writer.WriteStartObject("metadata");
            writer.WriteString("language", cell.Language == NotebookCellLanguage.Sql ? "sql" : "python");
            if (cell.IsParameterCell)
            {
                writer.WriteStartArray("tags");
                writer.WriteStringValue("parameters");
                writer.WriteEndArray();
            }
            writer.WriteStartObject("datateal");
            if (cell.DurationMs.HasValue)
                writer.WriteNumber("duration_ms", cell.DurationMs.Value);
            else
                writer.WriteNull("duration_ms");
            writer.WriteEndObject();
            writer.WriteEndObject(); // metadata

            writer.WriteStartArray("outputs");
            foreach (var output in cell.Outputs)
                WriteOutput(writer, output);
            if (cell.Error is { } err)
                WriteError(writer, err);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteOutput(Utf8JsonWriter writer, Output output)
    {
        writer.WriteStartObject();
        writer.WriteString("output_type", output.Type);

        if (output.Type == "stream")
        {
            writer.WriteString("name", output.Name ?? "stdout");
            writer.WriteString("text", output.Text ?? "");
        }
        else if (output.Type is "execute_result" or "display_data")
        {
            if (output.Type == "execute_result")
            {
                if (output.ExecutionCount.HasValue)
                    writer.WriteNumber("execution_count", output.ExecutionCount.Value);
                else
                    writer.WriteNull("execution_count");
            }

            writer.WriteStartObject("data");
            if (output.Data is not null)
            {
                foreach (var (key, value) in output.Data)
                {
                    writer.WritePropertyName(key);
                    if (value is JsonElement je)
                        je.WriteTo(writer);
                    else
                        writer.WriteStringValue(value?.ToString() ?? "");
                }
            }
            writer.WriteEndObject();

            writer.WriteStartObject("metadata");
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteError(Utf8JsonWriter writer, ErrorInfo err)
    {
        writer.WriteStartObject();
        writer.WriteString("output_type", "error");
        writer.WriteString("ename", err.Ename);
        writer.WriteString("evalue", err.Evalue);
        writer.WriteStartArray("traceback");
        foreach (var line in err.Traceback)
            writer.WriteStringValue(line);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
