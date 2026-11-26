using System.Globalization;
using System.Text.Json.Serialization;
using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Server;

// A piece of the building catalogue: its key (kept with every piece placed, so never renamed), the name the drawer shows,
// its place in the drawer, the game's form whose mesh it stands as, its scale and the plots it may stand on. Object and
// Form: what it stands as, and for the game's own furniture or container the form's id in hex, such as 0x0002F18A. Kit: the
// kit of house parts it belongs to (KitSettings), empty for none.
internal sealed class PieceSettings
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<PieceCategory>))]
    public PieceCategory Category { get; set; } = PieceCategory.Decoration;

    public string Asset { get; set; } = string.Empty;

    public float Scale { get; set; } = 1f;

    [JsonConverter(typeof(JsonStringEnumConverter<PieceUse>))]
    public PieceUse Use { get; set; } = PieceUse.Any;

    [JsonConverter(typeof(JsonStringEnumConverter<PieceObject>))]
    public PieceObject Object { get; set; } = PieceObject.Mesh;

    public string Form { get; set; } = string.Empty;

    public string Kit { get; set; } = string.Empty;

    // The form's id, 0 when there is none or it is no hex number.
    public uint ReadForm()
    {
        var digits = Form.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Form[2..] : Form;
        return uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var form) ? form : 0;
    }
}
