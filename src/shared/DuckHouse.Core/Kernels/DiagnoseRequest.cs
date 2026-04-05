namespace DuckHouse.Core.Kernels;

/// <param name="Context">Code from all prior cells, joined by newlines. Gives pyflakes/Jedi visibility into variables/imports defined earlier in the session.</param>
public record DiagnoseRequest(string Code, string Context = "");
