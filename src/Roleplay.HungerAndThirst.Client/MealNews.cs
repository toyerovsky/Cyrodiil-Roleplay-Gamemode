using ReadyM.Api.Idents;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Client;

/// <summary>A meal somebody nearby had (<see cref="MealEvent"/>): who fed whom, and what. <see cref="Food"/> is null
/// for a sip of water. <see cref="ByMe"/> and <see cref="ToMe"/> are both true for the player's own meal.</summary>
public sealed record MealNews(PlayerId Feeder, PlayerId Eater, Food? Food, bool ByMe, bool ToMe);
