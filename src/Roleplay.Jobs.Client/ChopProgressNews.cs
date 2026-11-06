using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>What <see cref="WoodcuttingRpc.ChopProgress"/> says about this player's hit.</summary>
public readonly record struct ChopProgressNews(string Kind, int Damage, int Health, float Wear);
