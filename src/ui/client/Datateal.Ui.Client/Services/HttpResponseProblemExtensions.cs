using System.Text.Json;

namespace Datateal.Ui.Client.Services;

/// <summary>
/// Drop-in async replacement for <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.
/// On a non-success response, extracts a user-friendly message from the body before throwing
/// so that callers (and the pages that display <c>ex.Message</c>) see something meaningful
/// instead of a raw HTTP status code.
/// </summary>
internal static class HttpResponseProblemExtensions
{
    /// <summary>
    /// Returns immediately on a success response.
    /// On a non-success response, attempts to extract a detail message from:
    /// <list type="number">
    ///   <item>RFC 9457 <c>ProblemDetails</c> — uses <c>detail</c>, then <c>title</c></item>
    ///   <item>Orchestrator plain-object format <c>{ "error": "…" }</c></item>
    /// </list>
    /// If neither yields a message, falls through to the standard
    /// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> so the caller receives
    /// the same <see cref="HttpRequestException"/> as before (with the original
    /// <see cref="HttpRequestException.StatusCode"/> intact, so transient-error checks
    /// in notebook/query pages continue to work).
    /// </summary>
    public static async Task EnsureSuccessWithDetailsAsync(
        this HttpResponseMessage response,
        CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? detail = null;

        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // RFC 9457 ProblemDetails: prefer "detail", fall back to "title"
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("detail", out var detailProp) &&
                        detailProp.ValueKind == JsonValueKind.String)
                    {
                        detail = detailProp.GetString();
                    }
                    else if (root.TryGetProperty("title", out var titleProp) &&
                             titleProp.ValueKind == JsonValueKind.String)
                    {
                        detail = titleProp.GetString();
                    }

                    // Orchestrator plain-object format: { "error": "…" }
                    if (string.IsNullOrWhiteSpace(detail) &&
                        root.TryGetProperty("error", out var errorProp) &&
                        errorProp.ValueKind == JsonValueKind.String)
                    {
                        detail = errorProp.GetString();
                    }
                }
            }
        }
        catch
        {
            // Body parse failed — fall through to EnsureSuccessStatusCode below.
        }

        if (!string.IsNullOrWhiteSpace(detail))
        {
            throw new HttpRequestException(detail, inner: null, statusCode: response.StatusCode);
        }

        // No useful body — delegate to the standard behaviour so callers get the
        // same HttpRequestException shape (with StatusCode) as before.
        response.EnsureSuccessStatusCode();
    }
}
