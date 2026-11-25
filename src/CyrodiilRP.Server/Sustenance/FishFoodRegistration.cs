using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Server;

namespace CyrodiilRP.Server.Sustenance;

// Makes the catch food in Hunger and Thirst as the server starts: a fish eaten from the game's inventory feeds.
[Service]
internal sealed partial class FishFoodRegistration(ISustenance sustenance)
{
    private void Start()
    {
        foreach (var food in FishFoods.All)
            sustenance.RegisterCustomFood(food);
    }
}
