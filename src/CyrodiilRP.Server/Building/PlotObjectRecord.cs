using System.Numerics;
using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Building;

// A plot_objects row as the server keeps it. Rotation: pitch, yaw and roll in degrees. RefId: the game's object it is, 0
// for a mesh.
internal sealed record PlotObjectRecord(long Id, long PlotId, string Piece, Vector3 Position, Vector3 Rotation, PlotObjectState State, uint RefId);
