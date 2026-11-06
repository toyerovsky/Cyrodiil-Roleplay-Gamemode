using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class ShippedConfigTests
{
    // The SDK's ModConfigReader options: an unknown key is an error, so a typo in config.json fails here, not on a server
    private static readonly JsonSerializerOptions SdkConfigOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    [Fact]
    public void The_shipped_config_parses_and_matches_the_defaults()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config.json"));

        var shipped = JsonSerializer.Deserialize<JobsConfig>(json, SdkConfigOptions)!;

        Assert.Equal(JsonSerializer.Serialize(new JobsConfig()), JsonSerializer.Serialize(shipped));
    }
}
