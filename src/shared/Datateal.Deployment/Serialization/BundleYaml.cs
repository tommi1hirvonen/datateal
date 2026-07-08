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

    public static T Deserialize<T>(string yaml) => Deserializer.Deserialize<T>(yaml);

    public static string Serialize<T>(T value) => Serializer.Serialize(value);
}
