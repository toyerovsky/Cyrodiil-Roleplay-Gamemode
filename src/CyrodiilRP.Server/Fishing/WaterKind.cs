namespace CyrodiilRP.Server.Fishing;

// The water a line is cast into, which decides the species that live there.
internal enum WaterKind
{
    LakeRumare,
    NibenBay,
    Sea,
    River,
    Lake,

    // Outside, in water the waters map does not name: streams, ponds and pools.
    Stream,

    // Any interior: caves, sewers, cellars.
    Underground,
}
