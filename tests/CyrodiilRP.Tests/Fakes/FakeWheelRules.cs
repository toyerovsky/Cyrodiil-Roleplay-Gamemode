using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Fakes;

// Texts are their keys, so a test reads which one was chosen.
internal sealed class FakeWheelRules : IWheelRules
{
    public HashSet<string> Permissions { get; } = new(StringComparer.Ordinal);

    public bool Muted { get; set; }

    public bool Near { get; set; } = true;

    public List<string> Notices { get; } = [];

    public bool Allows(OnlinePlayer actor, string permission) => Permissions.Contains(permission);

    public bool MaySpeak(OnlinePlayer actor) => !Muted;

    public bool AreNear(OnlinePlayer actor, OnlinePlayer target, float range) => Near;

    public string Translate(OnlinePlayer reader, Phrase text) => text.Key;

    public void Notify(OnlinePlayer to, Phrase text) => Notices.Add(text.Key);
}
