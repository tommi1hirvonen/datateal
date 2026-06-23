using System.Text.RegularExpressions;
using CronExpressionDescriptor;
using Quartz;

namespace Datateal.Ui.Client.Validation;

public static partial class ValidationHelper
{
    public const int MaxNodePoolNameLength = 100;

    // Python identifier: letter or underscore, followed by letters, digits, or underscores.
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex PythonIdentifierRegex();

    // Environment variable key: letters, digits, and underscores; must start with a letter or underscore.
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex EnvVarKeyRegex();

    private static readonly Options CronDescriptorOptions = new()
    {
        ThrowExceptionOnParseError = false,
        Use24HourTimeFormat = true,
        Locale = "en",
    };

    private static readonly HashSet<string> PythonKeywords = new(StringComparer.Ordinal)
    {
        "False", "None", "True", "and", "as", "assert", "async", "await",
        "break", "class", "continue", "def", "del", "elif", "else", "except",
        "finally", "for", "from", "global", "if", "import", "in", "is",
        "lambda", "nonlocal", "not", "or", "pass", "raise", "return",
        "try", "while", "with", "yield"
    };

    /// <summary>
    /// Validates a node name. Combines Kubernetes DNS label rules with AKS node pool name rules:
    /// <summary>
    /// Validates a node pool configuration name. The name is a user-facing label only;
    /// actual K8s node names are derived from pool IDs, not from this name.
    /// Returns an error message, or null if valid.
    /// </summary>
    public static string? ValidateNodePoolName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null; // empty is handled by "required" checks elsewhere

        if (name.Length > MaxNodePoolNameLength)
            return $"Name must be {MaxNodePoolNameLength} characters or fewer.";

        return null;
    }

    /// <summary>
    /// Validates a Python identifier (variable/parameter name).
    /// Returns an error message, or null if valid.
    /// </summary>
    public static string? ValidatePythonIdentifier(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null; // empty is handled by "required" checks elsewhere

        if (!PythonIdentifierRegex().IsMatch(name))
            return "Must start with a letter or underscore, followed by letters, digits, or underscores.";

        if (PythonKeywords.Contains(name))
            return $"'{name}' is a reserved Python keyword.";

        return null;
    }

    /// <summary>
    /// Validates a TimeSpan string (e.g. "00:00:30").
    /// Returns an error message, or null if valid.
    /// </summary>
    public static string? ValidateTimeSpan(string? value, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
            return required ? "This field is required." : null;

        return TimeSpan.TryParse(value, out _)
            ? null
            : "Enter a valid duration (e.g. 00:01:00 or 01:30:00).";
    }

    /// <summary>
    /// Validates a Quartz cron expression (6-field: seconds minutes hours day-of-month month day-of-week).
    /// Uses Quartz.CronExpression directly so validation matches what the scheduler will accept.
    /// Returns an error message, or null if valid.
    /// </summary>
    public static string? ValidateCronExpression(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
            return null; // required check handled by button Disabled

        try
        {
            _ = new CronExpression(cron.Trim());
            return null;
        }
        catch (FormatException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Returns a human-readable description of a Quartz cron expression, or an empty string if blank.
    /// Does not throw on invalid input.
    /// </summary>
    public static string GetCronDescription(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
            return string.Empty;

        return ExpressionDescriptor.GetDescription(cron.Trim(), CronDescriptorOptions);
    }

    /// <summary>
    /// Validates an environment variable key.
    /// Returns an error message, or null if valid.
    /// </summary>
    public static string? ValidateEnvVarKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (key.Length > 256)
            return "Key must be 256 characters or fewer.";

        if (!EnvVarKeyRegex().IsMatch(key))
        {
            if (char.IsDigit(key[0]))
                return "Key must not start with a digit.";
            return "Key may only contain letters, digits, and underscores.";
        }

        return null;
    }

    // DuckDB/Postgres catalog identifier: letter or underscore, followed by letters, digits, or underscores.
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex CatalogIdentifierRegex();

    /// <summary>
    /// Validates a catalog name. Must be a valid DuckDB identifier and a valid PostgreSQL database name:
    /// letters, digits, and underscores only; cannot start with a digit; max 63 characters (PostgreSQL limit);
    /// cannot start with "pg_" (reserved for PostgreSQL system catalogs).
    /// Returns an error message, or null if valid.
    /// </summary>
    public static string? ValidateCatalogName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (name.Length > 63)
            return "Catalog name must be 63 characters or fewer (PostgreSQL database name limit).";

        if (!CatalogIdentifierRegex().IsMatch(name))
        {
            if (char.IsDigit(name[0]))
                return "Catalog name must not start with a digit.";
            return "Catalog name may only contain letters, digits, and underscores.";
        }

        if (name.StartsWith("pg_", StringComparison.OrdinalIgnoreCase))
            return "Catalog name must not start with \"pg_\" (reserved for PostgreSQL system catalogs).";

        return null;
    }
}
