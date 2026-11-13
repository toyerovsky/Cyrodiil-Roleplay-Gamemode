using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>A crate deposit, as <see cref="DocksRpc.ZoneAvailable"/> tells it: Floor is the middle of its floor.</summary>
public sealed record DockZoneView(string Id, DockZoneKind Kind, Vector3 Floor, float Yaw);
