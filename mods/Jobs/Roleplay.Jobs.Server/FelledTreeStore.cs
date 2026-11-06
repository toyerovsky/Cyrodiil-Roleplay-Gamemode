using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Foliage;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

/// <summary>
/// A felled tree as saved: when it grows back, its own scale and the yaw it fell at, which place its stump and log.
/// A file from before those were saved reads them as 0, taken as scale 1 and a fall towards +X.
/// </summary>
public sealed record FelledTree(string Mesh, int X, int Y, int Z, DateTime GrowsBackUtc, float Scale, float FallYaw)
{
    public FoliageId Id => new(Mesh, X, Y, Z);

    public float TreeScale => Scale > 0f ? Scale : 1f;
}

/// <summary>
/// Felled trees and when each grows back, in <see cref="JobsConfig.SaveFile"/> next to the server DLL, so a restart
/// does not regrow the forest. Written whole on every change, through a temporary file.
/// </summary>
[Service]
public sealed partial class FelledTreeStore(JobsConfig config, ILogger logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // The relay is published AOT with reflection JSON off; naming a resolver opts this file back in.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private readonly Dictionary<FoliageId, FelledTree> _felled = [];
    private string? _path;

    public IReadOnlyCollection<FelledTree> Felled => _felled.Values;

    /// <summary>The server mod folder, read at once. Set by the entry point before any service starts.</summary>
    public void UseFolder(string folder)
    {
        _path = Path.Combine(folder, config.SaveFile);
        Load();
    }

    // Internal so the tests can load a file without the SDK starting the service
    internal void Load()
    {
        _felled.Clear();
        if (_path is null || !File.Exists(_path))
            return;

        try
        {
            foreach (var tree in JsonSerializer.Deserialize<List<FelledTree>>(File.ReadAllText(_path), Json) ?? [])
                _felled[tree.Id] = tree;
            logger.LogInformation("Jobs: {Count} felled trees loaded from {Path}.", _felled.Count, _path);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            var broken = _path + ".broken";
            File.Copy(_path, broken, overwrite: true);
            logger.LogWarning(exception, "Jobs: could not read {Path}, copied it to {Broken}; every tree stands again.", _path, broken);
        }
    }

    public void Add(FelledTree tree)
    {
        _felled[tree.Id] = tree;
        Save();
    }

    public void Remove(FoliageId id)
    {
        if (_felled.Remove(id))
            Save();
    }

    private void Save()
    {
        if (_path is null)
        {
            logger.LogError("Jobs: the felled trees cannot be saved, the store was never given its folder.");
            return;
        }

        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_felled.Values.ToList(), Json));
        File.Move(temporary, _path, overwrite: true);
    }
}
