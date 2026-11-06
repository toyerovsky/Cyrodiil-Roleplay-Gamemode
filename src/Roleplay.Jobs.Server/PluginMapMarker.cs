using System.Numerics;

namespace Roleplay.Jobs.Server;

/// <summary>A named map marker an Oblivion plugin places, where the game files put it, in its worldspace.</summary>
/// <param name="Worldspace">The worldspace's editor id, such as Tamriel; empty when the plugin does not say.</param>
/// <param name="Position">In game file units, as the plugin stores it.</param>
public sealed record PluginMapMarker(string Name, string Worldspace, Vector3 Position);
