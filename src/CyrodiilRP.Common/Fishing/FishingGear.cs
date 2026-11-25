namespace CyrodiilRP.Common.Fishing;

// The game has no fishing rod: its staves stand for one, a thinner one for each better grade, as the rod items on the
// ground and in the hand of a player whose game lacks the mode's own rods.
public static class FishingGear
{
    public static string StaffOf(RodTier tier) => tier switch
    {
        RodTier.Basic => "/Game/Art/Equipment/staffs/SM_Staff01.SM_Staff01",
        RodTier.Sturdy => "/Game/Art/Equipment/staffs/SM_Staff03.SM_Staff03",
        RodTier.Master => "/Game/Art/Equipment/staffs/SM_GrummiteObeliskPriest_Staff.SM_GrummiteObeliskPriest_Staff",
    };
}
