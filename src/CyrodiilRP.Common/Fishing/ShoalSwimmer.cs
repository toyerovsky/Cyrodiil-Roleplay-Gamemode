namespace CyrodiilRP.Common.Fishing;

// One small fish of the shoal round a float: its circle's radius, how fast it swims and which way round, how deep, where
// on the circle it starts (radians), how long its circle takes to swell and shrink, and how long it is drawn.
public readonly record struct ShoalSwimmer(
    float Radius,
    float Speed,
    bool Clockwise,
    float Depth,
    float Phase,
    float SwaySeconds,
    int LengthCm);
