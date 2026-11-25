namespace CyrodiilRP.Server.Fishing;

// How a species fights on the line: its strength from 0 to 1, how many seconds of good work tire it, how often it
// changes what it does, and the share of its runs that bring a jump.
internal sealed record FishFight(double Strength, double StaminaSeconds, double ChangeSoonestSeconds, double ChangeLatestSeconds, double Jumpiness);
