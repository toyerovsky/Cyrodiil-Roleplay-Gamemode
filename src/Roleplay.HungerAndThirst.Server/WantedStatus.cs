using OblivionMP.SDK.Common.Statuses;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>A status the character should have, and the vital it acts on, if any.</summary>
public readonly record struct WantedStatus(StatusEffect Effect, Stat? Vital);
