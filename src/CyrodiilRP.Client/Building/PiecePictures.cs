using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Building;

// The catalogue's pictures, drawn by this game from each piece's own meshes (ILocalProps.DrawPicture): one at a time
// while architect mode is open, each once a session, kept by the piece's asset and scale so a renamed key draws nothing
// again. The page gets each as a PNG data URL.
[Service]
internal sealed partial class PiecePictures(ILocalProps props, PawnWatch pawn, RpcHandlers rpc, ILogger logger)
{
    private const int Size = 256;
    private const string DataUrl = "data:image/png;base64,";

    private readonly Dictionary<PictureSubject, string> _drawn = [];
    private readonly HashSet<PictureSubject> _failed = [];
    private readonly Queue<string> _waiting = new();
    private readonly Dictionary<string, PictureSubject> _pieces = new(StringComparer.Ordinal);

    // A picture is being drawn: the next waits for it.
    private bool _drawing;

    public event Action<string, string>? Drawn;

    // Drawn only while someone looks at them.
    public bool Wanted { get; set; }

    private readonly record struct PictureSubject(string Asset, float Scale);

    // Every piece's picture drawn so far, by its key.
    public IEnumerable<KeyValuePair<string, string>> ListDrawn() =>
        _pieces.Where(pair => _drawn.ContainsKey(pair.Value)).Select(pair => KeyValuePair.Create(pair.Key, _drawn[pair.Value]));

    private void Start() => rpc.ArchitectCatalogueArrived += OnCatalogue;

    private void Stop() => rpc.ArchitectCatalogueArrived -= OnCatalogue;

    private void OnCatalogue(string[] keys, string[] assets, float[] scales)
    {
        if (assets.Length != keys.Length || scales.Length != keys.Length)
            return;

        _waiting.Clear();
        for (var index = 0; index < keys.Length; index++)
        {
            var subject = new PictureSubject(assets[index], scales[index]);
            _pieces[keys[index]] = subject;
            if (!_drawn.ContainsKey(subject) && !_failed.Contains(subject))
                _waiting.Enqueue(keys[index]);
        }
    }

    // The pieces the page shows now are drawn first, the rest after them.
    public void Prefer(IReadOnlyCollection<string> keys)
    {
        var shown = new HashSet<string>(keys, StringComparer.Ordinal);
        var waiting = _waiting.ToList();
        _waiting.Clear();
        foreach (var key in waiting.Where(shown.Contains).Concat(waiting.Where(key => !shown.Contains(key))))
            _waiting.Enqueue(key);
    }

    private void Update()
    {
        if (_drawing || !Wanted || !pawn.HasSettled(1f) || !_waiting.TryDequeue(out var key) || !_pieces.TryGetValue(key, out var subject))
            return;

        if (_drawn.TryGetValue(subject, out var known))
        {
            Drawn?.Invoke(key, known);
            return;
        }

        _drawing = true;
        props.DrawPicture(subject.Asset, subject.Scale, Size, png =>
        {
            _drawing = false;
            if (png is null)
            {
                _failed.Add(subject);
                logger.LogWarning("[CyrodiilRP] No picture could be drawn of the piece {Piece} ({Asset}).", key, subject.Asset);
                return;
            }

            var picture = DataUrl + Convert.ToBase64String(png);
            _drawn[subject] = picture;
            Drawn?.Invoke(key, picture);
        });
    }
}
