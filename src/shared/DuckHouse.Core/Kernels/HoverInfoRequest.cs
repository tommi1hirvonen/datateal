namespace DuckHouse.Core.Kernels;

/// <param name="Line">1-based line number (Jedi convention).</param>
/// <param name="Column">0-based column offset (Jedi convention).</param>
/// <param name="Context">Code from all prior cells, joined by newlines.</param>
public record HoverInfoRequest(string Code, int Line, int Column, string Context = "");
