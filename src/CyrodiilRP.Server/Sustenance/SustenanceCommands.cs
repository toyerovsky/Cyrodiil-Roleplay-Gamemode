using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;
using Roleplay.HungerAndThirst.Common;
using Roleplay.HungerAndThirst.Server;

namespace CyrodiilRP.Server.Sustenance;

// The staff's care for hunger and thirst, beside /aheal: Feed on the panel's player card.
[Service]
internal sealed partial class SustenanceCommands(CommandRegistry commands, IEntities entities, ISustenance sustenance, Audit audit)
{
    private const string FeedAction = "admin.feed";

    private void Start() => commands.AddStaff(StaffPermissions.Feed, AdminText.CmdFeed, Feed);

    private void Feed(CommandCall call)
    {
        var target = call.Sender;
        if (call.Words.Count > 0 && !call.TryTarget(0, out target))
            return;

        if (!entities.TryLookup(target.Id, out MainCharacter _))
        {
            call.Refuse(AdminText.NotInWorld(target.Name));
            return;
        }

        sustenance.SetMeters(target.Id, SustenanceScale.Max, SustenanceScale.Max);
        audit.Log(FeedAction, call.Sender.Ref, target.Ref);
        call.Reply(AdminText.Fed(target.Name));
    }
}
