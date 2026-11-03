using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Conditions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Interactions;

[Service]
internal sealed partial class WheelRules(StaffRoles roles, CharacterConditions conditions, Proximity proximity, RpChat chat) : IWheelRules
{
    public bool Allows(OnlinePlayer actor, string permission) => roles.Allows(actor, permission);

    public bool MaySpeak(OnlinePlayer actor) => conditions.CheckSpeech(actor) is null;

    public bool AreNear(OnlinePlayer actor, OnlinePlayer target, float range) => proximity.AreNear(actor.Id, target.Id, range);

    public string Translate(OnlinePlayer reader, Phrase text) => chat.Translate(reader, text);

    public void Notify(OnlinePlayer to, Phrase text) => chat.Notify(to, text);
}
