using OblivionMp.Sdk;
using OblivionMpCSharpMod;
using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Client;

// Tells the player in the game's own HUD when a meter drops into the next band, the same as the server's default
// tiers: hungry, starving, wasting away; thirsty, parched, dehydrated.
// Only on the way down, so eating never shows a warning. Also once at login if the saved meters are already low.
[Service]
public sealed partial class SustenanceWarnings(SustenanceState state, IUserInterface ui)
{
    private const float SecondsOnScreen = 5f;

    private static readonly string[] HungerLines = ["", "You are getting hungry.", "You are starving.", "Hunger is wearing you down."];
    private static readonly string[] ThirstLines = ["", "You are getting thirsty.", "You are parched.", "You are dying of thirst."];

    private int _hungerBand = -1;
    private int _thirstBand = -1;

    private void Update()
    {
        if (!state.Ready)
            return;
        Check(ref _hungerBand, state.Satiety, HungerLines);
        Check(ref _thirstBand, state.Hydration, ThirstLines);
    }

    private void Check(ref int band, double value, string[] lines)
    {
        int now = Band(value);
        if (now > band && now > 0)
            ui.ShowMessage(lines[now], MessagePosition.TopLeft, SecondsOnScreen);
        band = now;
    }

    // 0 fine, 1 low, 2 critical, 3 empty.
    private static int Band(double value) => value switch
    {
        <= 0 => 3,
        < SustenanceScale.Critical => 2,
        < SustenanceScale.Low => 1,
        _ => 0,
    };
}
