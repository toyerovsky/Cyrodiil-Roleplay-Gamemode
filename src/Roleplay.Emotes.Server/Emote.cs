using Roleplay.Emotes.Common;

namespace Roleplay.Emotes.Server;

/// <summary>An emote a player can play: one of the game's animations, or one a mod's pak adds.</summary>
/// <param name="Id">Unique among the emotes, as <c>wave</c> or <c>mymod.jig</c>. The wheel and
/// <see cref="EmotesRpc.AskEmote"/> name it by this.</param>
/// <param name="Name">What players read, in English.</param>
/// <param name="Animation">The third-person animation by object path, as
/// <c>/Game/Art/Animation/Humanoid/ThirdPerson/IdleAnims/A_Humanoid_Salute.A_Humanoid_Salute</c>.</param>
/// <param name="Kind">Whether <paramref name="Animation"/> is a montage or a sequence. The platform sends a montage
/// round to the other players on its own, a sequence the mod sends round, so a wrong kind shows it twice or never.</param>
public sealed record Emote(string Id, string Name, string Animation, AnimationKind Kind)
{
    /// <summary>The first-person animation played with it, or null for none: first person then shows nothing.</summary>
    public string? FirstPersonAnimation { get; init; }

    /// <summary>1 for the animation's own speed.</summary>
    public float PlayRate { get; init; } = 1f;

    /// <summary>Stops it after this many seconds, for a montage that loops until stopped. Null lets it play out.</summary>
    public float? Seconds { get; init; }

    /// <summary>Lower comes first on the wheel, the same in the order they were registered.</summary>
    public int Order { get; init; }

    /// <summary>A wheel icon's name, or null for the wheel's own emote icon. A name the wheel does not draw shows a
    /// question mark.</summary>
    public string? Icon { get; init; }

    /// <summary><see cref="Name"/> in other languages, by locale (<c>pl</c>).</summary>
    public IReadOnlyDictionary<string, string> Translations { get; init; } = new Dictionary<string, string>();

    /// <summary>The name in this locale, else the English one.</summary>
    public string NameIn(string locale) => Translations.TryGetValue(locale, out var name) ? name : Name;
}
