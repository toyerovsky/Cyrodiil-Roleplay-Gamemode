using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Lore;

// What of a card a reader may see. Everything another player gets of a card goes through here, on the server, so a
// page never holds a field its reader may not see.
internal static class LoreCuts
{
    public static LoreVisibility EffectiveVisibility(LoreFieldDefinition field, LoreValue value) =>
        field.Locked ? field.DefaultVisibility : value.Visibility;

    public static bool CanSee(LoreFieldDefinition field, LoreValue? value, LoreViewer viewer)
    {
        if (value is null || value.IsEmpty)
            return false;

        return viewer switch
        {
            LoreViewer.Owner or LoreViewer.Staff => true,
            LoreViewer.Acquaintance => !field.OwnerOnly,
            LoreViewer.Stranger => !field.OwnerOnly && EffectiveVisibility(field, value) == LoreVisibility.Public,
        };
    }

    // In the card's order.
    public static IEnumerable<LoreVisibleField> Visible(LoreCard card, LoreViewer viewer) =>
        LoreFields.All
            .Select(field => new LoreVisibleField(field, card.Get(field)!))
            .Where(item => CanSee(item.Field, item.Value, viewer));
}
