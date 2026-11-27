namespace CyrodiilRP.Client.Building;

// The gizmo as the architect page reads it: each point two numbers, x and y, a tip behind the view null.
internal sealed record GizmoView(float[] Origin, float[]? X, float[]? Y, float[]? Z);
