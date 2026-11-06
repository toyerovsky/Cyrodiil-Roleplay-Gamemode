namespace Roleplay.HungerAndThirst.Server;

// One line of SustenanceConfig.Foods. Item is the editor id from the SDK catalogue ("Apple", "DrinkAle") or a form
// id in hex ("0x00033680"). Name is what the satchel shows; it defaults to the item's default name or its editor id.
public sealed class FoodEntry
{
    public string Item { get; set; } = "";
    public string? Name { get; set; }
    public int Satiety { get; set; }
    public int Hydration { get; set; }
    public bool Remove { get; set; }
}
