using System.Text.RegularExpressions;

namespace Datateal.Deployment.Serialization;

/// <summary>
/// Resolves <c>${var.NAME}</c> and <c>${env.NAME}</c> tokens in string values.
/// <list type="bullet">
///   <item><c>${var.NAME}</c> — looks up <paramref name="variables"/> dictionary.</item>
///   <item><c>${env.NAME}</c> — looks up the process environment.</item>
/// </list>
/// Unresolved references throw <see cref="DeploymentVariableException"/>.
/// </summary>
public static class VariableSubstitution
{
    private static readonly Regex TokenPattern =
        new(@"\$\{(var|env)\.([^}]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Substitutes tokens in a single string value.
    /// Returns the original value if it contains no tokens.
    /// </summary>
    public static string Substitute(string value, IReadOnlyDictionary<string, string>? variables)
    {
        if (value is null || !value.Contains("${")) return value!;

        return TokenPattern.Replace(value, m =>
        {
            var kind = m.Groups[1].Value;
            var name = m.Groups[2].Value;

            if (kind == "var")
            {
                if (variables is not null && variables.TryGetValue(name, out var varVal))
                    return varVal;

                throw new DeploymentVariableException(
                    $"Bundle variable '${{var.{name}}}' is not defined. " +
                    "Provide it in the bundle's 'variables' section or supply it at deploy time.");
            }

            // kind == "env"
            var envVal = Environment.GetEnvironmentVariable(name);
            if (envVal is not null) return envVal;

            throw new DeploymentVariableException(
                $"Environment variable '${{env.{name}}}' ('{name}') is not set in the deployment environment.");
        });
    }

    /// <summary>
    /// Substitutes tokens in all string values of a dictionary.
    /// Returns null if the input is null.
    /// </summary>
    public static Dictionary<string, string>? SubstituteDict(
        Dictionary<string, string>? dict,
        IReadOnlyDictionary<string, string>? variables)
    {
        if (dict is null) return null;
        return dict.ToDictionary(kv => kv.Key, kv => Substitute(kv.Value, variables));
    }
}

/// <summary>Thrown when a <c>${var.*}</c> or <c>${env.*}</c> token cannot be resolved.</summary>
public class DeploymentVariableException(string message) : Exception(message);
