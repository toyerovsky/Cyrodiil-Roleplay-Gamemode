using CyrodiilRP.Common.Traders;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Traders;

// The name of each kind of the mode's people (trader, banker, stablemaster, mentors, registrar) by the origin the server
// stands it with, in the player's language, once the server has sent it.
[Service]
internal sealed partial class TraderLabelRegistry(RpcHandlers rpc)
{
    private readonly Dictionary<uint, string> _labels = [];

    public event Action<uint, string>? Changed;

    public bool IsEmpty => _labels.Count == 0;

    public string? Find(uint origin) => _labels.GetValueOrDefault(origin);

    private void Start()
    {
        rpc.TraderLabelled += OnTraderLabelled;
        rpc.BankerLabelled += OnBankerLabelled;
        rpc.StablemasterLabelled += OnStablemasterLabelled;
        rpc.MentorsLabelled += OnMentorsLabelled;
    }

    private void Stop()
    {
        rpc.TraderLabelled -= OnTraderLabelled;
        rpc.BankerLabelled -= OnBankerLabelled;
        rpc.StablemasterLabelled -= OnStablemasterLabelled;
        rpc.MentorsLabelled -= OnMentorsLabelled;
    }

    private void OnTraderLabelled(string label) => Set(TraderOrigin.SpawnerRefId, label);

    private void OnBankerLabelled(string label) => Set(TraderOrigin.BankerSpawnerRefId, label);

    private void OnStablemasterLabelled(string label) => Set(TraderOrigin.StablemasterSpawnerRefId, label);

    private void OnMentorsLabelled(MentorLabelSet labels)
    {
        Set(TraderOrigin.WarriorMentorSpawnerRefId, labels.Warrior);
        Set(TraderOrigin.MageMentorSpawnerRefId, labels.Mage);
        Set(TraderOrigin.SupportMentorSpawnerRefId, labels.Support);
        Set(TraderOrigin.RegistrarSpawnerRefId, labels.Registrar);
    }

    private void Set(uint origin, string label)
    {
        _labels[origin] = label;
        Changed?.Invoke(origin, label);
    }
}
