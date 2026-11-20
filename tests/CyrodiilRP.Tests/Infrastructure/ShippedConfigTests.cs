using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CyrodiilRP.Server;
using OblivionMP.SDK.Common.Skills;

namespace CyrodiilRP.Tests.Infrastructure;

public sealed class ShippedConfigTests
{
    // How the server reads a mod's config: names in any case, comments and trailing commas allowed, a key the class
    // does not have refused, and an enum as a number unless its property names a converter. A config it cannot read
    // stops the mod at start, and the smoke tests write a config of their own.
    private static readonly JsonSerializerOptions ServerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    [Fact]
    public void The_server_reads_the_shipped_config()
    {
        var shipped = File.ReadAllText(Path.Combine(Fakes.Repository.Root, "src", "CyrodiilRP.Server", "config.json"));

        var config = JsonSerializer.Deserialize<RpConfig>(shipped, ServerOptions);

        Assert.NotNull(config);
        Assert.Equal(PotionRule.PlayerMade, config.Specializations.Potions);
    }
}
