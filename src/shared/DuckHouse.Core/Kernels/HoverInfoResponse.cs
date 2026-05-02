namespace DuckHouse.Core.Kernels;

/// <param name="Contents">Ordered list of markdown strings to display in the hover popup. Empty means nothing to show.</param>
public record HoverInfoResponse(IReadOnlyList<string> Contents);
