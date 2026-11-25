namespace CyrodiilRP.Server.Fishing;

internal sealed record FishQuarry(FishKind Fish, double Share) : Quarry
{
    public override FishFight Fight => Fish.Fight;

    public override double SizeShare => Share;

    public int LengthCm => Fish.LengthAt(Share);

    public int Grams => Fish.GramsAt(LengthCm);

    public bool Trophy => Share >= FishingRules.TrophyFrom;

    public FishItem Item => new(Fish, Trophy);
}
