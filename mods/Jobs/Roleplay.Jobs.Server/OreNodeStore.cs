using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

/// <summary>What <see cref="OreNodeStore"/> keeps in its file.</summary>
public sealed class OreNodeSave
{
    // Nodes placed in game, beside the config's
    public List<OreNode> Placed { get; set; } = [];

    // Mined nodes by id, and when each grows back
    public Dictionary<string, DateTime> Mined { get; set; } = [];

}

/// <summary>
/// The nodes placed in game and the mined ones with when each grows back, in <see cref="JobsConfig.NodeSaveFile"/>
/// next to the server DLL, so a restart neither loses them nor refills the mines. Written whole on every change,
/// through a temporary file.
/// </summary>
[Service]
public sealed partial class OreNodeStore(JobsConfig config, ILogger logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // The relay is published AOT with reflection JSON off; naming a resolver opts this file back in.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private OreNodeSave _save = new();
    private string? _path;

    public IReadOnlyList<OreNode> Placed => _save.Placed;

    public IReadOnlyDictionary<string, DateTime> Mined => _save.Mined;

    /// <summary>The server mod folder, read at once. Set by the entry point before any service starts.</summary>
    public void UseFolder(string folder)
    {
        _path = Path.Combine(folder, config.NodeSaveFile);
        Load();
    }

    // Internal so the tests can load a file without the SDK starting the service
    internal void Load()
    {
        _save = new OreNodeSave();
        if (_path is null || !File.Exists(_path))
            return;

        try
        {
            _save = JsonSerializer.Deserialize<OreNodeSave>(File.ReadAllText(_path), Json) ?? new OreNodeSave();
            logger.LogInformation("Jobs: {Placed} placed ore nodes and {Mined} mined ones loaded from {Path}.",
                _save.Placed.Count, _save.Mined.Count, _path);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            var broken = _path + ".broken";
            File.Copy(_path, broken, overwrite: true);
            logger.LogWarning(exception, "Jobs: could not read {Path}, copied it to {Broken}; no placed nodes, none mined.", _path, broken);
        }
    }

    public void AddPlaced(OreNode node)
    {
        _save.Placed.Add(node);
        Save();
    }

    public bool RemovePlaced(string id)
    {
        var removed = _save.Placed.RemoveAll(node => OreYield.NodeId(node) == id) > 0;
        if (_save.Mined.Remove(id) || removed)
            Save();
        return removed;
    }

    public void SetMined(string id, DateTime growsBackUtc)
    {
        _save.Mined[id] = growsBackUtc;
        Save();
    }

    public void ClearMined(string id)
    {
        if (_save.Mined.Remove(id))
            Save();
    }

    private void Save()
    {
        if (_path is null)
        {
            logger.LogError("Jobs: the ore nodes cannot be saved, the store was never given its folder.");
            return;
        }

        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_save, Json));
        File.Move(temporary, _path, overwrite: true);
    }
}
