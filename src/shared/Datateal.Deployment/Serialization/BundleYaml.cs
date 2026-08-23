using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Datateal.Deployment.Serialization;

/// <summary>
/// Shared YamlDotNet serializer and deserializer instances configured for
/// Datateal bundle files:
/// <list type="bullet">
///   <item>snake_case keys via <c>UnderscoredNamingConvention</c></item>
///   <item>Null values omitted</item>
///   <item>Unknown properties ignored (forward-compatible)</item>
/// </list>
/// </summary>
public static class BundleYaml
{
    public static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
        .Build();

    public static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static T Deserialize<T>(string yaml)
    {
        try
        {
            return Deserializer.Deserialize<T>(yaml);
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not OperationCanceledException)
        {
            // YamlDotNet throws its own exception types (YamlException, etc.) for malformed YAML
            // or type-mapping failures. Translate them into InvalidOperationException so callers
            // (the deployment controller in particular) can uniformly treat "the uploaded bundle
            // is malformed" as a 400 Bad Request rather than an unhandled 500.
            throw new InvalidOperationException($"Bundle YAML could not be parsed: {ex.Message}", ex);
        }
    }

    public static string Serialize<T>(T value) => Serializer.Serialize(value);
}
