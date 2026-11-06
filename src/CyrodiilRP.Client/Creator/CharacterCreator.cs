using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Archetypes.Extensions;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Creator;

// The game's own creator, after the name form. The game says nothing when it closes, so it is watched every tick.
[Service]
internal sealed partial class CharacterCreator(
    RpcHandlers rpc,
    IPlayers players,
    IUserInterface ui,
    PawnWatch pawn,
    ILogger logger)
{
    // Asked for as a save finishes loading, the creator turns the camera to the face and hides the HUD but shows no window.
    // So it is asked for once the character has been in the world this long, and again this often until it is open.
    private const float SettleSeconds = 3f;
    private const float ReopenAfterSeconds = 3f;

    private Step _step;

    private float _lastOpenAttempt;

    private enum Step
    {
        Idle,
        OpeningCreator,
        InCreator,
    }

    private void Start()
    {
        rpc.NamingStarted += OnNamingStarted;
        rpc.AppearanceStarted += OnAppearanceStarted;
    }

    private void Stop()
    {
        rpc.NamingStarted -= OnNamingStarted;
        rpc.AppearanceStarted -= OnAppearanceStarted;
    }

    private void OnNamingStarted() => _step = Step.Idle;

    private void OnAppearanceStarted()
    {
        _step = Step.OpeningCreator;
        _lastOpenAttempt = float.NegativeInfinity;
    }

    private void Update()
    {
        switch (_step)
        {
            case Step.OpeningCreator when ui.IsCharacterCreatorOpen():
                _step = Step.InCreator;
                logger.LogInformation("[CyrodiilRP] The game's creator is open.");
                break;

            case Step.OpeningCreator
                when pawn.HasSettled(SettleSeconds) && Time.Elapsed - _lastOpenAttempt >= ReopenAfterSeconds:
                _lastOpenAttempt = Time.Elapsed;
                logger.LogInformation("[CyrodiilRP] Asking the game for its creator.");
                players.LocalMainCharacter?.OpenCharacterCreator();
                break;

            case Step.InCreator when !ui.IsCharacterCreatorOpen():
                _step = Step.Idle;
                logger.LogInformation("[CyrodiilRP] The game's creator closed.");
                rpc.SendFinishAppearance();
                break;
        }
    }
}
