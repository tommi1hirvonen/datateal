using Microsoft.CodeAnalysis;

namespace DuckHouse.Ui.Client.SourceGeneration;

public static class DiagnosticDescriptors
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "MicrosoftCodeAnalysisReleaseTracking", "RS2008:Enable analyzer release tracking", Justification = "<Pending>")]
    public static readonly DiagnosticDescriptor IncorrectModifiers
        = new("ICON001",
            "Incorrect modifiers for icon generation target symbol: {0}",
            "The target symbol for icon generation should be a public static partial class",
            "IconSourceGenerator",
            DiagnosticSeverity.Error,
            true);
}