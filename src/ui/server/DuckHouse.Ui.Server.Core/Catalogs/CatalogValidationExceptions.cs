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
    /// Validates that the name is both a valid DuckDB identifier and a valid PostgreSQL database name:
    /// letters, digits, and underscores only; cannot start with a digit; max 63 characters (PostgreSQL
    /// NAMEDATALEN limit); cannot start with "pg_" (reserved for PostgreSQL system catalogs).
    /// </summary>
    public static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CatalogNameValidationException(name, "it cannot be empty.");

        if (name.Length > 63)
            throw new CatalogNameValidationException(name,
                "it must be 63 characters or fewer (PostgreSQL database name limit).");

        if (!ValidIdentifierRegex().IsMatch(name))
            throw new CatalogNameValidationException(name,
                "it must contain only letters, digits, and underscores, and cannot start with a digit.");

        if (name.StartsWith("pg_", StringComparison.OrdinalIgnoreCase))
            throw new CatalogNameValidationException(name,
                "it must not start with \"pg_\" (reserved for PostgreSQL system catalogs).");
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
