using System.Security.Cryptography;
using System.Text;

namespace Datateal.Core.ApiTokens;

/// <summary>
/// Generates and hashes API token secrets. Tokens are 256-bit random values with a recognizable
/// <c>dtl_</c> prefix (aids git secret-scanning). Only the SHA-256 hash is persisted; the
/// plaintext is shown to the creator once and never stored.
/// </summary>
public static class ApiTokenGenerator
{
    public const string Prefix = "dtl_";

    /// <summary>Number of leading characters retained as the stored/display prefix.</summary>
    public const int PrefixLength = 12;

    /// <summary>Creates a new random token, returning the plaintext, its stored prefix, and its hash.</summary>
    public static (string Token, string TokenPrefix, string TokenHash) Generate()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        var token = Prefix + Base64UrlEncode(buffer);
        return (token, GetPrefix(token), ComputeHash(token));
    }

    /// <summary>Computes the SHA-256 hash (upper-case hex) of a token string.</summary>
    public static string ComputeHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    /// <summary>Returns the stored/display prefix for a token string.</summary>
    public static string GetPrefix(string token) =>
        token.Length <= PrefixLength ? token : token[..PrefixLength];

    /// <summary>Constant-time comparison of two token hashes.</summary>
    public static bool HashesEqual(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    /// <summary>True if the value looks like a Datateal API token.</summary>
    public static bool LooksLikeToken(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
