using System.Numerics;
using OblivionMP.SDK;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// What the angler alone hears of the line, at the character: the reel while the server has it reeling, the line
// straining in the red, and each catch or loss once. A rare catch also glows in the hand.
[Service]
internal sealed partial class AnglerCues(ISounds sounds, IVisualEffects effects)
{
    private const string Sounds = "/Game/WwiseAudio/";
    private const string ReelSound = Sounds + "Game_Object/Trap/obj_trp_gears_slow.obj_trp_gears_slow";
    private const string StrainSound = Sounds + "Game_Object/Trap/obj_trp_rope_stretch.obj_trp_rope_stretch";
    private const string SnapSound = Sounds + "Game_Object/Trap/obj_trp_tripwire_snap.obj_trp_tripwire_snap";
    private const string CatchSound = Sounds + "Interface/Pickup/ui_itm_pickup_ingredient_up.ui_itm_pickup_ingredient_up";
    private const string RareSound = Sounds + "Interface/HUD/Redesign/ui_hud_area_new.ui_hud_area_new";
    private const string LevelSound = Sounds + "Interface/HUD/Redesign/ui_hud_skill_increase.ui_hud_skill_increase";
    private const string Glow = "/Game/Art/FX/Environment/NS_Dynamic_ObjectGlow.NS_Dynamic_ObjectGlow";
    private const float GlowScale = 0.5f;
    private const float GlowSeconds = 3f;
    private const float ReelFadeSeconds = 0.15f;
    private const float StrainEverySeconds = 1.2f;

    private SoundHandle? _reel;
    private float _strainedAt = float.NegativeInfinity;
    // The last outcome heard, or null until the first view of this fishing says where the count stands.
    private int? _heard;

    public void Begin(AnglerView? view) => _heard = view is null ? null : view.Outcome?.Number ?? 0;

    // Each outcome once: one already there when the fishing began is not heard again.
    public void Hear(AnglerView view, Vector3 at, Vector3 hand)
    {
        if (_heard is not { } heard)
        {
            _heard = view.Outcome?.Number ?? 0;
            return;
        }

        if (view.Outcome is not { } outcome || outcome.Number <= heard)
            return;

        _heard = outcome.Number;
        if (SoundOf(outcome.Kind) is { } sound)
            sounds.PlayAt(sound, at);
        if (outcome.Rare)
        {
            sounds.PlayAt(RareSound, at);
            effects.Play(Glow, hand, Vector3.Zero, GlowScale, GlowSeconds);
        }

        if (outcome.LevelUp is not null)
            sounds.PlayAt(LevelSound, at);
    }

    public void Keep(AnglerView? view, Vector3 at, float now)
    {
        var fight = view is { Stage: LineStage.Fight, Fight: { } held } ? held : null;
        Reel(fight is { Reeling: true }, at);
        if (fight is { TensionWarn: > 0f } line && line.Tension >= line.TensionWarn && now - _strainedAt >= StrainEverySeconds)
        {
            sounds.PlayAt(StrainSound, at);
            _strainedAt = now;
        }
    }

    public void End()
    {
        Reel(reeling: false, Vector3.Zero);
        _heard = null;
        _strainedAt = float.NegativeInfinity;
    }

    private void Reel(bool reeling, Vector3 at)
    {
        if (reeling && _reel is null)
            _reel = sounds.PlayAt(ReelSound, at);
        else if (!reeling && _reel is { } playing)
        {
            sounds.Stop(playing, ReelFadeSeconds);
            _reel = null;
        }
    }

    private static string? SoundOf(LineOutcome kind) => kind switch
    {
        LineOutcome.Caught or LineOutcome.Found => CatchSound,
        LineOutcome.LostSnapped => SnapSound,
        LineOutcome.LostEarly or LineOutcome.LostNibble or LineOutcome.LostLate or LineOutcome.LostSlack or LineOutcome.LostRunaway => null,
    };
}
