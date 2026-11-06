using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>What <see cref="MiningRpc.MiningProgress"/> says about this player's hit.</summary>
public readonly record struct MiningProgressNews(string What, int Damage, int Health, float Wear);
