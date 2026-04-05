namespace DuckHouse.Core.Kernels;

/// <param name="Line">1-based line number (Jedi convention).</param>
/// <param name="Column">0-based column number (Jedi convention).</param>
/// <param name="Context">Code from all prior cells, joined by newlines. Gives Jedi visibility into variables/imports defined earlier in the session.</param>
public record CompleteRequest(string Code, int Line, int Column, string Context = "");
