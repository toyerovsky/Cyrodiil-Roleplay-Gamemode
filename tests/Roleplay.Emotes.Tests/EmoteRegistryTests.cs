using Roleplay.Emotes.Common;
using Roleplay.Emotes.Server;

namespace Roleplay.Emotes.Tests;

public sealed class EmoteRegistryTests
{
    private const string Path = "/Game/Mod/AM_Jig.AM_Jig";

    private readonly EmoteRegistry _registry = new();

    [Fact]
    public void Emotes_come_by_order_and_the_same_order_as_registered()
    {
        _registry.Add(Create("late", order: 500));
        _registry.Add(Create("first"));
        _registry.Add(Create("second"));
        _registry.Add(Create("middle", order: 100));

        Assert.Equal(["first", "second", "middle", "late"], _registry.All.Select(e => e.Id));
    }

    [Fact]
    public void An_id_taken_twice_is_refused()
    {
        _registry.Add(Create("jig"));

        Assert.Throws<InvalidOperationException>(() => _registry.Add(Create("jig", order: 3)));
        Assert.Single(_registry.All);
    }

    [Fact]
    public void A_removed_emote_is_found_no_more()
    {
        _registry.Add(Create("jig"));

        Assert.True(_registry.Remove("jig"));
        Assert.False(_registry.Remove("jig"));
        Assert.Null(_registry.Find("jig"));
    }

    [Theory]
    [InlineData("Game/Mod/AM_Jig.AM_Jig")]
    [InlineData("/Game/Mod/AM_Jig")]
    [InlineData("")]
    public void An_animation_that_is_no_object_path_is_refused(string animation)
    {
        Assert.ThrowsAny<ArgumentException>(() => _registry.Add(new Emote("jig", "Jig", animation, AnimationKind.Montage)));
    }

    [Fact]
    public void An_emote_without_a_kind_a_rate_or_a_name_is_refused()
    {
        Assert.Throws<ArgumentException>(() => _registry.Add(new Emote("jig", "Jig", Path, default)));
        Assert.Throws<ArgumentException>(() => _registry.Add(Create("jig") with { PlayRate = 0f }));
        Assert.Throws<ArgumentException>(() => _registry.Add(Create("jig") with { Seconds = 0f }));
        Assert.ThrowsAny<ArgumentException>(() => _registry.Add(Create("jig") with { Name = " " }));
        Assert.Empty(_registry.All);
    }

    [Fact]
    public void A_name_comes_in_the_reader_s_language_or_in_english()
    {
        var emote = Create("jig") with { Translations = new Dictionary<string, string> { ["pl"] = "Giga" } };

        Assert.Equal("Giga", emote.NameIn("pl"));
        Assert.Equal("Jig", emote.NameIn("de"));
    }

    [Fact]
    public void The_game_s_emotes_all_register_with_a_polish_name()
    {
        foreach (var emote in VanillaEmotes.All)
            _registry.Add(emote);

        Assert.Equal(VanillaEmotes.All.Count, _registry.All.Count);
        Assert.All(VanillaEmotes.All, emote => Assert.NotEqual(emote.Name, emote.NameIn("pl")));
        Assert.All(VanillaEmotes.All, emote => Assert.Equal(emote.Kind == AnimationKind.Montage, emote.Animation.Contains("/AM_", StringComparison.Ordinal)));
    }

    private static Emote Create(string id, int order = 0) => new(id, "Jig", Path, AnimationKind.Montage) { Order = order };
}
