using System.Text.RegularExpressions;

namespace DuckHouse.Ui.Server.Core.Catalogs;

/// <summary>
/// Thrown when a catalog name fails validation.
/// </summary>
public sealed partial class CatalogNameValidationException(string name, string reason)
    : InvalidOperationException($"The catalog name \"{name}\" is not valid: {reason}")
{
    public string Name { get; } = name;

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex ValidIdentifierRegex();

    /// <summary>
    /// Validates that the name is a valid DuckDB identifier
    /// (alphanumeric + underscores, cannot start with a digit, non-empty).
    /// </summary>
    public static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CatalogNameValidationException(name, "it cannot be empty.");

        if (!ValidIdentifierRegex().IsMatch(name))
            throw new CatalogNameValidationException(name,
                "it must contain only letters, digits, and underscores, and cannot start with a digit.");
    }
}

/// <summary>
/// Thrown when a catalog name already exists.
/// </summary>
public sealed class CatalogNameConflictException(string name)
    : InvalidOperationException($"A catalog named \"{name}\" already exists.")
{
    public string Name { get; } = name;
}
