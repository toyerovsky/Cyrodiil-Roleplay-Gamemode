namespace Roleplay.Jobs.Common;

/// <summary>What a weapon hit struck, as the client reports it: the SDK's StruckSurface, which this side cannot see.</summary>
public enum StruckKind : byte
{
    Nothing = 0,
    Landscape = 1,
    Foliage = 2,
    Other = 3,
}
