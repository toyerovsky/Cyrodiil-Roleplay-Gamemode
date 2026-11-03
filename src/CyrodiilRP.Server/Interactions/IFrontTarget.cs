using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Interactions;

// What else than a player the interaction key reaches, such as a trader. The nearest thing in front gets the key.
internal interface IFrontTarget
{
    // Null when none is within reach.
    float? MeasureInFront(OnlinePlayer actor, float reach, float coneDegrees);

    void Use(OnlinePlayer actor, float reach, float coneDegrees);
}
