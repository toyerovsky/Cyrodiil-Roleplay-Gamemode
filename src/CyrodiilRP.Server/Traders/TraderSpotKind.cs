namespace CyrodiilRP.Server.Traders;

// What the person standing at a spot does for a player.
internal enum TraderSpotKind
{
    // Sells the wares of a shop.
    Shop,

    // Keeps a character's gold (Bank/).
    Banker,

    // Sells the game's horses (Stable/).
    Stablemaster,

    // Teaches the warrior's specializations (Specializations/).
    WarriorMentor,

    // Teaches the mage's specializations.
    MageMentor,

    // Teaches the support's specializations.
    SupportMentor,

    // Teaches every specialization, in the Imperial Palace.
    Registrar,
}
