using System.Numerics;

namespace CyrodiilRP.Client.Building;

// The gizmo as the screen shows it, in shares of the view from its top left: its middle and the tip of each arrow, null
// for a tip behind the view.
internal readonly record struct GizmoOnScreen(Vector2 Origin, Vector2? X, Vector2? Y, Vector2? Z);
