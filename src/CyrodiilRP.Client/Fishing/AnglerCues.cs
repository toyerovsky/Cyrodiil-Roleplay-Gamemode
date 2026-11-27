using System.Numerics;
using CyrodiilRP.Common.Fishing;
using OblivionMP.SDK;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// What the angler alone hears of the line, at the character: the reel's ratchet while the server has it reeling a
// resting fish and its drag while a fish runs, the line straining in the red, and each catch or loss once. A rare catch
// also glows in the hand.
[Service]
internal sealed partial class AnglerCues(ISounds sounds, IVisualEffects effects)
{
    private const string Sounds = "/Game/WwiseAudio/";
    // The game has no reel: the lockpick's tumblers click like one. The ratchet lasts 1.37 to 1.64 s and the drag 0.6 to
    // 0.7 s, so each is played again just before its shortest ends.
    private const string RatchetSound = Sounds + "Interface/Lockpick/ui_lock_tumbler_move.ui_lock_tumbler_move";
    private const float RatchetEverySeconds = 1.35f;
    private const string DragSound = Sounds + "Interface/MiniGames/Redesign/ui_lockpicking_tumbler_fast.ui_lockpicking_tumbler_fast";
    private const float DragEverySeconds = 0.58f;
    private const string StrainSound = Sounds + "Game_Object/Trap/obj_trp_rope_stretch.obj_trp_rope_stretch";
    private const string SnapSound = Sounds + "Game_Object/Trap/obj_trp_tripwire_snap.obj_trp_tripwire_snap";
    private const string CatchSound = Sounds + "Interface/Pickup/ui_itm_pickup_ingredient_up.ui_itm_pickup_ingredient_up";
    private const string RareSound = Sounds + "Interface/HUD/Redesign/ui_hud_area_new.ui_hud_area_new";
    private const string LevelSound = Sounds + "Interface/HUD/Redesign/ui_hud_skill_increase.ui_hud_skill_increase";
    private const string Glow = "/Game/Art/FX/Environment/NS_Dynamic_ObjectGlow.NS_Dynamic_ObjectGlow";
    private const float GlowScale = 0.5f;
    private const float GlowSeconds = 3f;
    private const float ReelFadeSeconds = 0.15f;
    // The line creaks this often as its red begins, and this often about to snap.
    private const float StrainEverySeconds = 1.2f;
    private const float StrainEveryAtSnap = 0.45f;

    private SoundHandle? _reel;
    private ReelNoise _reelNoise = ReelNoise.Silent;
    private float _reelPlayedAt = float.NegativeInfinity;
    private float _strainedAt = float.NegativeInfinity;
    // The last outcome heard, or null until the first view of this fishing says where the count stands.
    private int? _heard;
    // The level the last outcome heard brought, if any: letting the catch go may bring one after.
    private int? _heardLevel;

    public void Begin(AnglerView? view)
    {
        _heard = view is null ? null : view.Outcome?.Number ?? 0;
        _heardLevel = view?.Outcome?.LevelUp;
    }

    // Each outcome once: one already there when the fishing began is not heard again.
    public void Hear(AnglerView view, Vector3 at, Vector3 hand)
    {
        if (_heard is not { } heard)
        {
            _heard = view.Outcome?.Number ?? 0;
            _heardLevel = view.Outcome?.LevelUp;
            return;
        }

        if (view.Outcome is not { } outcome)
            return;

        if (outcome.Number <= heard)
        {
            // The same catch again, let go: it may have reached a level the catch itself did not.
            if (outcome.Number == heard && outcome.LevelUp is { } level && level != _heardLevel)
            {
                _heardLevel = level;
                sounds.PlayAt(LevelSound, at);
            }

            return;
        }

        _heard = outcome.Number;
        _heardLevel = outcome.LevelUp;
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
        Reel(NoiseOf(fight), at, now);
        if (fight is { TensionWarn: > 0f } line && line.Tension >= line.TensionWarn && now - _strainedAt >= StrainEvery(line))
        {
            sounds.PlayAt(StrainSound, at);
            _strainedAt = now;
        }
    }

    public void End()
    {
        Reel(ReelNoise.Silent, Vector3.Zero, float.NegativeInfinity);
        _heard = null;
        _heardLevel = null;
        _strainedAt = float.NegativeInfinity;
    }

    private static float StrainEvery(AnglerFight line) =>
        StrainEverySeconds + (StrainEveryAtSnap - StrainEverySeconds) * AnglerFraming.RedShare(line.Tension, line.TensionWarn);

    // A change of noise fades the last one out; the same noise is played again on its beat.
    private void Reel(ReelNoise noise, Vector3 at, float now)
    {
        if (noise == _reelNoise && (noise == ReelNoise.Silent || now - _reelPlayedAt < EveryOf(noise)))
            return;

        if (noise != _reelNoise && _reel is { } playing)
            sounds.Stop(playing, ReelFadeSeconds);

        _reelNoise = noise;
        _reel = ReelSoundOf(noise) is { } sound ? sounds.PlayAt(sound, at) : null;
        _reelPlayedAt = now;
    }

    private static ReelNoise NoiseOf(AnglerFight? fight) => fight switch
    {
        null => ReelNoise.Silent,
        { Pull: LinePull.Left or LinePull.Right or LinePull.Away } => ReelNoise.Drag,
        { Pull: LinePull.Rest, Reeling: true } => ReelNoise.Ratchet,
        { Pull: LinePull.Rest, Reeling: false } => ReelNoise.Silent,
    };

    private static string? ReelSoundOf(ReelNoise noise) => noise switch
    {
        ReelNoise.Silent => null,
        ReelNoise.Ratchet => RatchetSound,
        ReelNoise.Drag => DragSound,
    };

    private static float EveryOf(ReelNoise noise) => noise switch
    {
        ReelNoise.Silent => float.PositiveInfinity,
        ReelNoise.Ratchet => RatchetEverySeconds,
        ReelNoise.Drag => DragEverySeconds,
    };

    private static string? SoundOf(LineOutcome kind) => kind switch
    {
        LineOutcome.Caught or LineOutcome.Found => CatchSound,
        LineOutcome.LostSnapped => SnapSound,
        LineOutcome.Released or LineOutcome.LostEarly or LineOutcome.LostNibble or LineOutcome.LostLate or LineOutcome.LostSlack
            or LineOutcome.LostRunaway => null,
    };
}
