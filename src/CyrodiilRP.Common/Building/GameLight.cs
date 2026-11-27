namespace CyrodiilRP.Common.Building;

// The light a game light form gives: its colour, how far it reaches in centimetres, how bright in candelas, and where on
// its mesh it shines from.
public sealed record GameLight(byte Red, byte Green, byte Blue, float Radius, float Candelas, LightHang Hang);
