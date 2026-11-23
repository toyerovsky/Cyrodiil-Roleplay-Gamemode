using System.Reflection;
using OblivionMP.SDK.Common.Items;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Tests.Fakes;

// The custom items a test says exist, named as the SDK names them: form id 0 and a form entity of their own.
internal sealed class FakeCustomItems : ICustomItems
{
    private readonly List<DefinedCustomItem> _defined = [];

    public NetworkFormId Add(string key, CustomItem item)
    {
        var id = CreateId((uint)_defined.Count + 1);
        _defined.Add(new DefinedCustomItem(key, item, id));
        return id;
    }

    public void Define(string key, CustomItem item) =>
        throw new NotSupportedException("A test adds its custom items with Add, as the SDK has them once their entity exists.");

    public bool TryGet(string key, out NetworkFormId item)
    {
        var found = _defined.FirstOrDefault(defined => defined.Key == key);
        item = found.Id;
        return found.Key is not null;
    }

    public IReadOnlyList<DefinedCustomItem> ListDefined() => _defined;

    public bool TryFind(NetworkFormId item, out DefinedCustomItem found)
    {
        found = _defined.FirstOrDefault(defined => defined.Id == item);
        return found.Key is not null;
    }

    // The form entity's network id is the SDK's own (internal), as a custom item's id is in the game.
    private static NetworkFormId CreateId(uint entity)
    {
        var field = typeof(NetworkFormId).GetField("FormEntityNetId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("NetworkFormId no longer has FormEntityNetId: name a custom item another way.");
        var netId = Activator.CreateInstance(field.FieldType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [default(PlayerId), entity], null);
        object id = default(NetworkFormId);
        field.SetValue(id, netId);
        return (NetworkFormId)id;
    }
}
