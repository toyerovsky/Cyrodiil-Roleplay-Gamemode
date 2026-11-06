using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;
using Roleplay.HungerAndThirst.Server;

namespace CyrodiilRP.Server.Characters;

// Level, attributes, skills, bounty and fame stay the save's: the server cannot reset them (DEVELOPMENT.md). The game's
// creator has ended any spell or potion at work.
[Service]
internal sealed partial class FreshStart(IEntities entities, ISustenance sustenance)
{
    public bool TryApply(PlayerId player)
    {
        if (!entities.TryLookup(player, out MainCharacter main))
            return false;

        var character = EntityHandle.Of(main);
        var equipment = character.As<Equipment>();
        StartingKit.Dress(equipment);
        equipment.CurrentPoison = default;

        var quiver = character.As<Quiver>();
        quiver.HasQuiver = false;
        quiver.ArrowCount = 0;

        // The game takes a vital as its share of the maximum, so full is the maximum the character has now.
        var vitals = character.As<Vitals>();
        vitals.Hp = vitals.HpBase;
        vitals.Magicka = vitals.MagickaBase;
        vitals.Fatigue = vitals.FatigueBase;

        // Hunger and thirst are kept per account, so a new character would carry on from the last one's.
        sustenance.Refill(player);
        return true;
    }
}
