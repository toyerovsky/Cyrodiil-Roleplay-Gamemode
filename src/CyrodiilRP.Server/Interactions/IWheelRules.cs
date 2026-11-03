using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Interactions;

// What the wheels ask of the rest of the server.
internal interface IWheelRules
{
    bool Allows(OnlinePlayer actor, string permission);

    bool MaySpeak(OnlinePlayer actor);

    bool AreNear(OnlinePlayer actor, OnlinePlayer target, float range);

    string Translate(OnlinePlayer reader, Phrase text);

    void Notify(OnlinePlayer to, Phrase text);
}
