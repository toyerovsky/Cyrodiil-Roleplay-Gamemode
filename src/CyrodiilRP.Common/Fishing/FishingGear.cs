namespace CyrodiilRP.Common.Fishing;

// The game has no fishing rod: its staves stand for one, in the angler's hand and as the rod items on the ground, a
// thinner one for each better grade.
public static class FishingGear
{
    public static string MeshOf(RodTier tier) => tier switch
    {
        RodTier.Basic => "/Game/Art/Equipment/staffs/SM_Staff01.SM_Staff01",
        RodTier.Sturdy => "/Game/Art/Equipment/staffs/SM_Staff03.SM_Staff03",
        RodTier.Master => "/Game/Art/Equipment/staffs/SM_GrummiteObeliskPriest_Staff.SM_GrummiteObeliskPriest_Staff",
    };
}
