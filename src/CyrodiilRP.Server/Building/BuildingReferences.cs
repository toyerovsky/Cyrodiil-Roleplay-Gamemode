using System.Numerics;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Serverside;
using OblivionMP.SDK.Serverside.Spawners;
using OblivionMP.SDK.Serverside.World;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Building;

// The pieces that are the game's own objects: furniture to sit on or sleep in, containers to open and doors to swing, made
// by the server in the cell they stand in under a ref id of its own, and kept by that cell's save. Update thread only.
[Service]
internal sealed partial class BuildingReferences(IEntities entities, WorldRefs refs, ServerRefIds refIds, Plots plots, ILogger logger)
{
    private void Start() => plots.Loaded += SweepDoors;

    private void Stop() => plots.Loaded -= SweepDoors;

    // A door taken away before Delete knew doors stayed in its cell's save with no piece to hold it: a door the server made
    // of a door the catalogue lists, that no piece holds, goes.
    private void SweepDoors(IReadOnlySet<uint> held)
    {
        var doorForms = plots.Pieces.All.Where(piece => piece.Object == PieceObject.Door)
            .Select(piece => (NetworkFormId)new FormId(piece.FormId))
            .ToHashSet();
        var loose = new List<Door>();
        foreach (var door in entities.Query<Door>())
        {
            if (ServerRefIds.IsServerMade(door.RefId) && doorForms.Contains(door.BaseId) && !held.Contains(door.RefId))
                loose.Add(door);
        }

        foreach (var door in loose)
        {
            logger.LogWarning("[CyrodiilRP] Door {RefId:X} was the building's, but no piece holds it any more: it is taken away.", door.RefId);
            entities.Delete(door);
        }
    }

    // An entity's Rotation is the game's heading less 90 degrees, which is how a character's pawn faces. An object's actor
    // faces its heading itself (its mesh is the old model with Y flipped), so it stood a quarter turn off the preview of the
    // same blueprint: the object is given the piece's yaw less 90.
    private const float ObjectYawOffset = -90f;

    // The ref id of the object made, 0 when no cell there is loaded to make it in.
    public uint Create(Piece piece, OnlinePlayer near, Vector3 position, float yaw)
    {
        if (!TryFindCell(near, position, out var cell))
            return 0;

        var refId = refIds.Next();
        switch (piece.Object)
        {
            case PieceObject.Furniture:
                var furniture = entities.Create<Furniture>(cell, cell.MasterClient);
                furniture.RefId = refId;
                furniture.BaseId = new FormId(piece.FormId);
                furniture.Position = position;
                furniture.Rotation = yaw + ObjectYawOffset;
                break;
            case PieceObject.Container:
                var container = entities.Create<Container>(cell, cell.MasterClient);
                container.RefId = refId;
                container.BaseId = new FormId(piece.FormId);
                container.Position = position;
                container.Rotation = yaw + ObjectYawOffset;
                break;
            case PieceObject.Door:
                var door = entities.Create<Door>(cell, cell.MasterClient);
                door.RefId = refId;
                door.BaseId = new FormId(piece.FormId);
                door.Position = position;
                door.Rotation = yaw + ObjectYawOffset;
                break;
            case PieceObject.Mesh:
                throw new ArgumentOutOfRangeException(nameof(piece), piece.Object, "A mesh is no object of the game's.");
            default:
                throw new ArgumentOutOfRangeException(nameof(piece), piece.Object, "Not what a piece stands as.");
        }

        return refId;
    }

    // False when there is no such object any more.
    public bool Move(uint refId, Vector3 position, float yaw)
    {
        if (!refs.TryFind(refId, out var entity) || !entity.TryAs<Transform>(out var transform))
            return false;

        transform.Position = position;
        transform.Rotation = yaw + ObjectYawOffset;
        return true;
    }

    // False when there is no such object any more.
    public bool Delete(uint refId)
    {
        if (!refs.TryFind(refId, out var entity))
            return false;

        if (entity.TryAs<Furniture>(out var furniture))
            return entities.Delete(furniture);

        if (entity.TryAs<Container>(out var container))
            return entities.Delete(container);

        if (entity.TryAs<Door>(out var door))
            return entities.Delete(door);

        logger.LogError("[CyrodiilRP] Ref {RefId:X} of a piece is no furniture, container or door, and stays.", refId);
        return false;
    }

    // The exterior cell the spot is in, or the one the architect stands in (an interior, or a cell not loaded).
    private bool TryFindCell(OnlinePlayer near, Vector3 position, out Cell cell)
    {
        cell = default;
        if (!entities.TryLookup(near.Id, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
            return false;

        var outside = new FullCellId(area.AreaId, GameSpace.ToExteriorCell(position));
        if (entities.TryLookup(outside, out cell) && cell.MasterClient != PlayerId.Invalid)
            return true;

        return entities.TryLookup(main.As<ParentCell>().ToFullCellId(area.AreaId), out cell) && cell.MasterClient != PlayerId.Invalid;
    }
}
