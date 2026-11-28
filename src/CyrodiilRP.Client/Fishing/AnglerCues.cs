using System.Numerics;
using OblivionMP.SDK;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// What the angler alone hears of the line, at the character: the hook going in, the reel's ratchet while the server has
// it reeling a resting fish and its drag while a fish takes line, an alarm in their place while the line is forced or in
// the red, each twitch of the bait by what it came to, and each catch or loss once. A rare catch also glows in the hand.
[Service]
internal sealed partial class AnglerCues(ISounds sounds, IVisualEffects effects)
{
    private const string Sounds = "/Game/WwiseAudio/";
    // The game has no reel. The lockpick's slow tumblers click like one, softly: 14 to 21 clicks in 0.92 to 1.08 s.
    private const string RatchetSound = Sounds + "Interface/MiniGames/Redesign/ui_lockpicking_tumbler_slow_very.ui_lockpicking_tumbler_slow_very";
    private const float RatchetEverySeconds = 1.1f;
    // Hot iron quenched: a hiss with no click in it, 20 dB over the ratchet for 0.65 s and as far down again by 2 s.
    private const string DragSound = Sounds + "Character/Foley/char_fol_forge_quench.char_fol_forge_quench";
    private const float DragEverySeconds = 2f;
    // A trap's candle switch: a tink, then a squeak rising from 1.7 to 2.25 kHz that ends by 0.6 s, 25 dB over the ratchet.
    private const string AlarmSound = Sounds + "Game_Object/Trap/obj_trp_switch_candle.obj_trp_switch_candle";
    // Begun again as that squeak ends, so the alarm never reaches the pause and the falling squeak after it.
    private const float AlarmEverySeconds = 0.6f;
    // The speech wheel's rollover, a ting at 2.8 kHz gone in 0.1 s, and a book put down, a thud under 600 Hz as loud.
    private const string TwitchGoodSound = Sounds + "Interface/Global/ui_glb_speech_rollover.ui_glb_speech_rollover";
    private const string TwitchBadSound = Sounds + "Interface/Pickup/ui_itm_pickup_book_down.ui_itm_pickup_book_down";
    // Lockpicks picked up: steel ringing at 7.7 kHz, loudest 0.2 s in and gone by 0.3 s.
    private const string StrikeSound = Sounds + "Interface/Pickup/ui_itm_pickup_lockpick_up.ui_itm_pickup_lockpick_up";
    private const string SnapSound = Sounds + "Game_Object/Trap/obj_trp_tripwire_snap.obj_trp_tripwire_snap";
    private const string CatchSound = Sounds + "Interface/Pickup/ui_itm_pickup_ingredient_up.ui_itm_pickup_ingredient_up";
    private const string RareSound = Sounds + "Interface/HUD/Redesign/ui_hud_area_new.ui_hud_area_new";
    private const string LevelSound = Sounds + "Interface/HUD/Redesign/ui_hud_skill_increase.ui_hud_skill_increase";
    private const string Glow = "/Game/Art/FX/Environment/NS_Dynamic_ObjectGlow.NS_Dynamic_ObjectGlow";
    private const float GlowScale = 0.5f;
    private const float GlowSeconds = 3f;
    private const float FadeSeconds = 0.15f;

    private SoundHandle? _reel;
    private ReelNoise _reelNoise = ReelNoise.Silent;
    private float _reelPlayedAt = float.NegativeInfinity;
    private SoundHandle? _alarm;
    private float _alarmPlayedAt = float.NegativeInfinity;
    // The last outcome heard, or null until the first view of this fishing says where the count stands.
    private int? _heard;
    // The level the last outcome heard brought, if any: letting the catch go may bring one after.
    private int? _heardLevel;
    private bool _fighting;
    // The twitches of the cast as last told, or null until this fishing is told of them first.
    private int? _twitches;

    public void Begin(AnglerView? view)
    {
        _heard = view is null ? null : view.Outcome?.Number ?? 0;
        _heardLevel = view?.Outcome?.LevelUp;
        _fighting = view is { Stage: LineStage.Fight };
        _twitches = null;
    }

    // The hook going in and each outcome once: a fight or an outcome already there when the fishing began is not heard.
    public void Hear(AnglerView view, Vector3 at, Vector3 hand)
    {
        var hooked = view.Stage == LineStage.Fight && !_fighting;
        _fighting = view.Stage == LineStage.Fight;
        if (_heard is not { } heard)
        {
            _heard = view.Outcome?.Number ?? 0;
            _heardLevel = view.Outcome?.LevelUp;
            return;
        }

        if (hooked)
            sounds.PlayAt(StrikeSound, at);

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

    // Each twitch of the bait once, by whether it helped. The count starts over with a cast, and the first told is only kept.
    public void HearTwitch(int count, bool helped, Vector3 at)
    {
        if (_twitches is { } before && count > before)
            sounds.PlayAt(helped ? TwitchGoodSound : TwitchBadSound, at);
        _twitches = count;
    }

    // The alarm takes the reel's place, so its clicks and its hiss never cover it.
    public void Keep(AnglerView? view, Vector3 at, float now)
    {
        var fight = view is { Stage: LineStage.Fight, Fight: { } held } ? held : null;
        var strained = fight is { TensionWarn: > 0f } line && (IsForced(line) || line.Tension >= line.TensionWarn);
        Alarm(strained, at, now);
        Reel(strained ? ReelNoise.Silent : NoiseOf(fight), at, now);
    }

    // Reeling into a run, heard from its first moment, which the server still lets pass.
    private static bool IsForced(AnglerFight line) => line.Reeling && line.Pull switch
    {
        LinePull.Left or LinePull.Right or LinePull.Away => true,
        LinePull.Rest => false,
    };

    public void End()
    {
        Reel(ReelNoise.Silent, Vector3.Zero, float.NegativeInfinity);
        Alarm(sounding: false, Vector3.Zero, float.NegativeInfinity);
        _heard = null;
        _heardLevel = null;
        _fighting = false;
        _twitches = null;
    }

    // Begun again on its beat, the squeak before it cut, and faded out once the line is neither forced nor in the red.
    private void Alarm(bool sounding, Vector3 at, float now)
    {
        if (!sounding)
        {
            if (_alarm is { } last)
                sounds.Stop(last, FadeSeconds);
            _alarm = null;
            return;
        }

        if (_alarm is { } playing)
        {
            if (now - _alarmPlayedAt < AlarmEverySeconds)
                return;

            sounds.Stop(playing, FadeSeconds);
        }

        _alarm = sounds.PlayAt(AlarmSound, at);
        _alarmPlayedAt = now;
    }

    // A noise begins again on its beat, and another at once: either way what played before it fades out.
    private void Reel(ReelNoise noise, Vector3 at, float now)
    {
        if (noise == _reelNoise && (noise == ReelNoise.Silent || now - _reelPlayedAt < EveryOf(noise)))
            return;

        if (_reel is { } playing)
            sounds.Stop(playing, FadeSeconds);

        _reelNoise = noise;
        _reel = ReelSoundOf(noise) is { } sound ? sounds.PlayAt(sound, at) : null;
        _reelPlayedAt = now;
    }

    // A run the rod leans against takes no line.
    private static ReelNoise NoiseOf(AnglerFight? fight) => fight switch
    {
        null => ReelNoise.Silent,
        { Pull: LinePull.Left, Lean: 1, Reeling: false } or { Pull: LinePull.Right, Lean: -1, Reeling: false } => ReelNoise.Silent,
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
