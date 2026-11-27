using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Lore;
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
        config.Progression.RequireValid();
        config.Zones.RequireValid();
        Assert.Equal(PotionRule.PlayerMade, config.Specializations.Potions);
    }

    // The docs tell a server owner to write the lore mode by name, and the shipped config leaves it out.
    [Fact]
    public void The_server_reads_the_lore_mode_by_name()
    {
        var config = JsonSerializer.Deserialize<RpConfig>("""{ "lore": { "catalogMode": "CanonPlus" } }""", ServerOptions);

        Assert.Equal(LoreCatalogMode.CanonPlus, config!.Lore.CatalogMode);
    }

    // A section the shipped config leaves out is read only on a server that sets it, so every enum is checked here.
    [Fact]
    public void Every_enum_of_the_config_is_read_by_name()
    {
        var unnamed = ListEnumProperties(typeof(RpConfig), [])
            .Where(property => property.GetCustomAttribute<JsonConverterAttribute>() is null)
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}");

        Assert.Empty(unnamed);
    }

    [Fact]
    public void A_lore_mode_outside_the_list_stops_the_mod()
    {
        var settings = new LoreSettings { CatalogMode = (LoreCatalogMode)int.MaxValue };

        Assert.Throws<InvalidOperationException>(settings.RequireValid);
    }

    private static IEnumerable<PropertyInfo> ListEnumProperties(Type type, HashSet<Type> seen)
    {
        if (!seen.Add(type))
            yield break;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite))
        {
            var valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (valueType.IsEnum)
            {
                yield return property;
                continue;
            }

            var inner = valueType.IsGenericType ? valueType.GetGenericArguments() : [valueType];
            foreach (var nested in inner.Where(nested => nested.Assembly == typeof(RpConfig).Assembly && nested.IsClass))
            {
                foreach (var found in ListEnumProperties(nested, seen))
                    yield return found;
            }
        }
    }
}
