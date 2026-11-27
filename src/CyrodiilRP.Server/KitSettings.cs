using System.Text;
using System.Text.Json.Serialization;
using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Server;

// A kit of the game's house parts in the architect's drawer: its key, the name the drawer shows, the plots its parts may
// stand on, and its meshes, each by its folder under the game's architecture ("bruma/Bruma_HouseLowerExt_Kit/SM_..."). Each
// mesh is a piece of its own, keyed by the mesh's name, which the game keeps.
internal sealed class KitSettings
{
    public const string KeyPrefix = "kit.";

    private const string MeshPrefix = "SM_";
    private const string Architecture = "/Game/Art/Environment/Architecture/";

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<PieceUse>))]
    public PieceUse Use { get; set; } = PieceUse.Any;

    public List<string> Meshes { get; set; } = [];

    public IEnumerable<PieceSettings> ToPieces() => Meshes.Select(path =>
    {
        var mesh = MeshOf(path);
        return new PieceSettings
        {
            Key = KeyOf(mesh),
            Name = NameOf(mesh),
            Category = PieceCategory.Kits,
            Asset = $"{Architecture}{path}.{mesh}",
            Scale = 1f,
            Use = Use,
            Kit = Key,
        };
    });

    // The mesh's own name: the last part of its path.
    public static string MeshOf(string path) => path[(path.LastIndexOf('/') + 1)..];

    public static string KeyOf(string mesh) =>
        KeyPrefix + (mesh.StartsWith(MeshPrefix, StringComparison.OrdinalIgnoreCase) ? mesh[MeshPrefix.Length..] : mesh).ToLowerInvariant();

    // SM_brumahouselower01_RoofSupports_A is "Roof supports A (01)": its words after the house's own name, and the house's
    // number. A mesh with nothing after the house's name keeps it.
    public static string NameOf(string mesh)
    {
        var bare = mesh.StartsWith(MeshPrefix, StringComparison.OrdinalIgnoreCase) ? mesh[MeshPrefix.Length..] : mesh;
        var parts = bare.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return bare;

        var words = string.Join(' ', parts.Skip(1).SelectMany(SplitWords));
        words = char.ToUpperInvariant(words[0]) + words[1..];
        var number = new string(parts[0].Reverse().TakeWhile(char.IsAsciiDigit).Reverse().ToArray());
        return number.Length > 0 ? $"{words} ({number})" : words;
    }

    // RoofSupports is "roof supports", a letter on its own stays one: A.
    private static IEnumerable<string> SplitWords(string part)
    {
        if (part.Length == 1)
            return [part.ToUpperInvariant()];

        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var letter in part)
        {
            if (char.IsUpper(letter) && word.Length > 0)
            {
                words.Add(word.ToString().ToLowerInvariant());
                word.Clear();
            }

            word.Append(letter);
        }

        words.Add(word.ToString().ToLowerInvariant());
        return words;
    }
}
