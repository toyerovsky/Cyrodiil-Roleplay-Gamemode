using Roleplay.Emotes.Common;

namespace Roleplay.Emotes.Server;

/// <summary>
/// The game's own idles that make emotes, registered from the start. The montages are the NPCs' idles and play full
/// body; the sequences play in the UpperBody slot (see <see cref="AnimationKind"/>). A mod's own come after them by
/// <see cref="Emote.Order"/>: these take 0 to 99.
/// </summary>
public static class VanillaEmotes
{
    private const string IdleAnims = "/Game/Art/Animation/Humanoid/ThirdPerson/IdleAnims/";
    private const string DlcAnims = "/Game/Art/Animation/Humanoid/ThirdPerson/DlcAnims/";
    private const string Polish = "pl";

    public static IReadOnlyList<Emote> All { get; } =
    [
        Sequence("wave", IdleAnims, "A_Humanoid_Follow_Me", "Beckon", "Przywołaj"),
        Sequence("halt", IdleAnims, "A_Humanoid_Halt", "Halt", "Stój"),
        Sequence("salute", IdleAnims, "A_Humanoid_Salute", "Salute", "Salutuj"),
        Sequence("cheer", IdleAnims, "A_Humanoid_Cheer01", "Cheer", "Wiwatuj"),
        Sequence("point", IdleAnims, "A_Humanoid_Point_Forward", "Point", "Wskaż"),
        Sequence("point_up", IdleAnims, "A_Humanoid_Point_Upward", "Point up", "Wskaż w górę"),
        Sequence("offer", IdleAnims, "A_Humanoid_Give_Item", "Offer", "Podaj"),
        Sequence("proclaim", IdleAnims, "A_Humanoid_Speech_HandsInAir", "Proclaim", "Przemów"),
        Sequence("exasperated", IdleAnims, "A_Humanoid_Exasperated", "Exasperated", "Zniecierpliw się"),
        Sequence("exhausted", IdleAnims, "A_Humanoid_Exhausted", "Exhausted", "Zmęczenie"),
        Sequence("sulk", IdleAnims, "A_Humanoid_Arms_Crossed_Sulking", "Sulk", "Dąsaj się"),
        Sequence("look_around", IdleAnims, "A_Humanoid_Scan", "Look around", "Rozejrzyj się"),
        Sequence("ponder", IdleAnims, "A_Humanoid_Ponder_01", "Ponder", "Zamyśl się"),
        Sequence("genuflect", IdleAnims, "A_Humanoid_Genuflect", "Genuflect", "Przyklęknij"),
        Sequence("yield", IdleAnims, "A_Humanoid_OneHand_Yield", "Yield", "Poddaj się"),
        Sequence("mages_salute", IdleAnims, "A_Humanoid_Salute_MagesGuild", "Mages Guild salute", "Salut Gildii Magów"),
        Sequence("dance", DlcAnims, "A_Humanoid_Dance", "Dance", "Tańcz"),
        Sequence("juggle", DlcAnims, "A_Humanoid_SpecialIdle_Juggling", "Juggle", "Żongluj"),
        Sequence("take_notes", DlcAnims, "A_Humanoid_TakeNotes", "Take notes", "Notuj"),
        Montage("pray", "AM_Humanoid_Praying", "Pray", "Módl się"),
        Montage("kneel", "AM_Humanoid_OneHand_Kneeling_Sowrd_Down", "Kneel", "Uklęknij"),
        Montage("cower", "AM_Humanoid_Cower", "Cower", "Kul się"),
        Montage("arms_crossed", "AM_Humanoid_Talk_ArmsCrossed_Still", "Cross arms", "Skrzyżuj ręce"),
        Montage("read", "AM_Humanoid_Read_Book", "Read", "Czytaj"),
        Montage("drink", "AM_Humanoid_Drink_Mug_Idle", "Drink", "Pij"),
        Montage("drunk", "AM_Humanoid_Drunk", "Stagger", "Zatocz się"),
    ];

    private static Emote Sequence(string id, string folder, string asset, string name, string polish) =>
        Create(id, folder, asset, AnimationKind.Sequence, name, polish);

    private static Emote Montage(string id, string asset, string name, string polish) =>
        Create(id, IdleAnims, asset, AnimationKind.Montage, name, polish);

    private static Emote Create(string id, string folder, string asset, AnimationKind kind, string name, string polish) =>
        new(id, name, $"{folder}{asset}.{asset}", kind)
        {
            Translations = new Dictionary<string, string> { [Polish] = polish },
        };
}
