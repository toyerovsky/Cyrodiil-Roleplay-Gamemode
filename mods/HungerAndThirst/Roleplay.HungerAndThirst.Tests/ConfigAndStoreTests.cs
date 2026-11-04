using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Roleplay.HungerAndThirst.Server;
using Xunit;

namespace Roleplay.HungerAndThirst.Tests;

public class ConfigAndStoreTests
{
    // The same options as the SDK's ModConfigReader: an unknown key is an error, so a typo in config.json, or a key
    // renamed in SustenanceConfig but not in the file, fails here instead of on a live server.
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
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config.json"));

        SustenanceConfig shipped = JsonSerializer.Deserialize<SustenanceConfig>(json, SdkConfigOptions)!;
        SustenanceConfig defaults = new();

        string[] lists = [nameof(SustenanceConfig.Foods), nameof(SustenanceConfig.Hunger), nameof(SustenanceConfig.Thirst)];
        foreach (var property in typeof(SustenanceConfig).GetProperties().Where(p => !lists.Contains(p.Name)))
            Assert.Equal(property.GetValue(defaults), property.GetValue(shipped));
        Assert.Empty(shipped.Foods);
        Assert.Equal(JsonSerializer.Serialize(defaults.Hunger), JsonSerializer.Serialize(shipped.Hunger));
        Assert.Equal(JsonSerializer.Serialize(defaults.Thirst), JsonSerializer.Serialize(shipped.Thirst));
    }

    [Fact]
    public void Saved_meters_come_back_after_a_restart()
    {
        string folder = Directory.CreateTempSubdirectory("hat-store-").FullName;
        try
        {
            Guid account = Guid.NewGuid();
            SustenanceStore first = new(new SustenanceConfig(), NullLogger.Instance);
            first.UseFolder(folder);
            first.Load();
            first.Put(account, new SavedMeters(420, 77));
            first.Save();

            SustenanceStore second = new(new SustenanceConfig(), NullLogger.Instance);
            second.UseFolder(folder);
            second.Load();

            Assert.True(second.TryGet(account, out SavedMeters meters));
            Assert.Equal(new SavedMeters(420, 77), meters);
            Assert.False(File.Exists(Path.Combine(folder, "sustenance.json.tmp")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_broken_save_is_kept_aside_and_the_server_starts_fresh()
    {
        string folder = Directory.CreateTempSubdirectory("hat-store-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "sustenance.json"), "{ not json");
            SustenanceStore store = new(new SustenanceConfig(), NullLogger.Instance);
            store.UseFolder(folder);
            store.Load();

            Assert.False(store.TryGet(Guid.NewGuid(), out _));
            Assert.True(File.Exists(Path.Combine(folder, "sustenance.json.broken")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
