using Datateal.Core.ApiTokens;

namespace Datateal.Core.Tests.ApiTokens;

/// <summary>
/// Tests the pure API-token secret generation/hashing helpers and token validity logic. These
/// underpin the API-token authentication boundary: only high-entropy random tokens are issued,
/// only their hashes are persisted, and comparisons are constant-time.
/// </summary>
public class ApiTokenGeneratorTests
{
    [Fact]
    public void Generate_ProducesPrefixedToken_WithConsistentPrefixAndHash()
    {
        var (token, prefix, hash) = ApiTokenGenerator.Generate();

        Assert.StartsWith(ApiTokenGenerator.Prefix, token);
        Assert.Equal(ApiTokenGenerator.GetPrefix(token), prefix);
        Assert.Equal(ApiTokenGenerator.ComputeHash(token), hash);
        Assert.Equal(ApiTokenGenerator.PrefixLength, prefix.Length);
    }

    [Fact]
    public void Generate_IsUnique_AcrossInvocations()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => ApiTokenGenerator.Generate().Token).ToList();
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    [Fact]
    public void ComputeHash_IsDeterministic_AndDiffersPerToken()
    {
        var (tokenA, _, _) = ApiTokenGenerator.Generate();
        var (tokenB, _, _) = ApiTokenGenerator.Generate();

        Assert.Equal(ApiTokenGenerator.ComputeHash(tokenA), ApiTokenGenerator.ComputeHash(tokenA));
        Assert.NotEqual(ApiTokenGenerator.ComputeHash(tokenA), ApiTokenGenerator.ComputeHash(tokenB));
    }

    [Fact]
    public void HashesEqual_MatchesForIdenticalHashes_AndDiffersOtherwise()
    {
        var (token, _, hash) = ApiTokenGenerator.Generate();

        Assert.True(ApiTokenGenerator.HashesEqual(hash, ApiTokenGenerator.ComputeHash(token)));
        Assert.False(ApiTokenGenerator.HashesEqual(hash, ApiTokenGenerator.ComputeHash(token + "x")));
    }

    [Theory]
    [InlineData("dtl_abc", true)]
    [InlineData("dtl_", true)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LooksLikeToken_RecognizesPrefix(string? value, bool expected)
    {
        Assert.Equal(expected, ApiTokenGenerator.LooksLikeToken(value));
    }
}

public class ApiTokenTests
{
    private static ApiToken Token(bool revoked = false, DateTime? validFrom = null, DateTime? validTo = null) =>
        new()
        {
            Name = "t",
            TokenPrefix = "dtl_xxxxxxxx",
            TokenHash = "HASH",
            ValidFrom = validFrom ?? DateTime.UtcNow.AddDays(-1),
            ValidTo = validTo,
            IsRevoked = revoked,
        };

    [Fact]
    public void IsActive_True_WhenWithinWindowAndNotRevoked()
    {
        Assert.True(Token().IsActive(DateTime.UtcNow));
    }

    [Fact]
    public void IsActive_False_WhenRevoked()
    {
        Assert.False(Token(revoked: true).IsActive(DateTime.UtcNow));
    }

    [Fact]
    public void IsActive_False_BeforeValidFrom()
    {
        var now = DateTime.UtcNow;
        Assert.False(Token(validFrom: now.AddHours(1)).IsActive(now));
    }

    [Fact]
    public void IsActive_False_AfterValidTo()
    {
        var now = DateTime.UtcNow;
        Assert.False(Token(validTo: now.AddHours(-1)).IsActive(now));
    }

    [Fact]
    public void IsActive_True_WhenValidToIsNull()
    {
        Assert.True(Token(validTo: null).IsActive(DateTime.UtcNow));
    }
}
