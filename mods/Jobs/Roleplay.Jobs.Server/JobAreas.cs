using System.Numerics;
using Microsoft.Extensions.Logging;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The places marked for a job on this server: quarries, and lumber camps. Each comes from the config's list and, when
/// switched on, one around every map marker whose name holds the job's marker name in the plugins the server hands its
/// players (<see cref="JobsConfig.PluginFolders"/>), in the worldspaces <see cref="JobsConfig.MarkerWorldspaces"/>
/// lists. Read once, when the entry point gives the server mod folder.
/// </summary>
[Service]
public sealed partial class JobAreas(JobsConfig config, ILogger logger)
{
    private readonly List<MarkedArea> _quarries = [];
    private readonly List<MarkedArea> _lumberCamps = [];

    public IReadOnlyList<MarkedArea> Quarries => _quarries;

    public IReadOnlyList<MarkedArea> LumberCamps => _lumberCamps;

    /// <summary>The server mod folder, which <see cref="JobsConfig.PluginFolders"/> are relative to.</summary>
    public void UseFolder(string folder)
    {
        var mining = config.Mining;
        var woodcutting = config.Woodcutting;
        _quarries.Clear();
        _lumberCamps.Clear();
        _quarries.AddRange(FromConfig(mining.Quarries, mining.QuarryRadius));
        _lumberCamps.AddRange(FromConfig(woodcutting.LumberCamps, woodcutting.LumberCampRadius));

        // A lumber camp only matters where trees are kept to camps: no need to read every plugin for one otherwise
        var campsFromMarkers = woodcutting.LumberCampsOnly && woodcutting.LumberCampsFromMarkers;
        if (mining.QuarriesFromMarkers || campsFromMarkers)
        {
            var markers = PluginsIn(folder).SelectMany(Markers).ToList();
            if (mining.QuarriesFromMarkers)
                _quarries.AddRange(Named(markers, mining.QuarryMarkerName, mining.QuarryRadius));
            if (campsFromMarkers)
                _lumberCamps.AddRange(Named(markers, woodcutting.LumberCampMarkerName, woodcutting.LumberCampRadius));
        }

        foreach (var quarry in _quarries)
            logger.LogInformation("Jobs: quarry {Name}, {Radius:0} cm around {Center}.", quarry.Name, quarry.Radius, quarry.Center);
        foreach (var camp in _lumberCamps)
            logger.LogInformation("Jobs: lumber camp {Name}, {Radius:0} cm around {Center}.", camp.Name, camp.Radius, camp.Center);
        if (woodcutting.LumberCampsOnly && _lumberCamps.Count == 0)
            logger.LogWarning("Jobs: lumberCampsOnly is on but there is no lumber camp: no tree can be felled.");
    }

    private static IEnumerable<MarkedArea> FromConfig(IEnumerable<AreaConfig> areas, float defaultRadius)
        => areas.Select(area => new MarkedArea(area.Name, new Vector3(area.X, area.Y, area.Z), area.Radius > 0f ? area.Radius : defaultRadius));

    private static IEnumerable<MarkedArea> Named(IEnumerable<(PluginMapMarker Marker, string Plugin)> markers, string name, float radius)
        => markers
            .Where(found => name.Length > 0 && found.Marker.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(found => new MarkedArea(found.Marker.Name, TesUnrealSpace.ToUnrealLocation(found.Marker.Position), radius));

    // Every plugin under the folders, the first one found of each name
    private IEnumerable<string> PluginsIn(string folder)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relative in config.PluginFolders)
        {
            var pluginFolder = Path.GetFullPath(Path.Combine(folder, relative));
            if (!Directory.Exists(pluginFolder))
            {
                logger.LogInformation("Jobs: no plugin folder at {Folder}.", pluginFolder);
                continue;
            }

            var plugins = Directory.EnumerateFiles(pluginFolder, "*.esp", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(pluginFolder, "*.esm", SearchOption.AllDirectories))
                .Order(StringComparer.OrdinalIgnoreCase);
            foreach (var plugin in plugins)
            {
                if (seen.Add(Path.GetFileName(plugin)))
                    yield return plugin;
            }
        }
    }

    // The plugin's named map markers in the worldspaces that count
    private IEnumerable<(PluginMapMarker Marker, string Plugin)> Markers(string plugin)
    {
        IReadOnlyList<PluginMapMarker> markers;
        try
        {
            markers = PluginMapMarkers.Read(File.ReadAllBytes(plugin));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            logger.LogWarning(exception, "Jobs: could not read the map markers of {Plugin}; it marks no quarries or lumber camps.", plugin);
            yield break;
        }

        logger.LogInformation("Jobs: {Plugin} has {Count} named map markers.", plugin, markers.Count);
        foreach (var marker in markers)
        {
            if (config.MarkerWorldspaces.Contains(marker.Worldspace, StringComparer.OrdinalIgnoreCase))
            {
                yield return (marker, plugin);
                continue;
            }
            if (IsJobMarker(marker.Name))
                logger.LogInformation("Jobs: map marker {Name} in {Plugin} is in worldspace '{Worldspace}', which markerWorldspaces does not list. Skipped.",
                    marker.Name, Path.GetFileName(plugin), marker.Worldspace);
        }
    }

    private bool IsJobMarker(string name)
        => Holds(name, config.Mining.QuarryMarkerName) || Holds(name, config.Woodcutting.LumberCampMarkerName);

    private static bool Holds(string name, string part) => part.Length > 0 && name.Contains(part, StringComparison.OrdinalIgnoreCase);
}
