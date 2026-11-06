using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>An ore node standing, as <see cref="MiningRpc.NodeAvailable"/> tells it.</summary>
public readonly record struct OreNodeView(string Id, string Kind, string Asset, Vector3 Position, float Yaw, float Scale);
