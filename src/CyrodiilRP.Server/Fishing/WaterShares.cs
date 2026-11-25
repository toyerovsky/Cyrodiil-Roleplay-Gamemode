namespace CyrodiilRP.Server.Fishing;

// How many of a species live in each kind of water, against the others there: 0 for none.
internal sealed record WaterShares(double LakeRumare, double NibenBay, double Sea, double River, double Lake, double Stream, double Underground)
{
    public double Of(WaterKind water) => water switch
    {
        WaterKind.LakeRumare => LakeRumare,
        WaterKind.NibenBay => NibenBay,
        WaterKind.Sea => Sea,
        WaterKind.River => River,
        WaterKind.Lake => Lake,
        WaterKind.Stream => Stream,
        WaterKind.Underground => Underground,
    };
}
