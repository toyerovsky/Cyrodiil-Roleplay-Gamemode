using CyrodiilRP.Data.Building;
using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Server;

internal sealed class BuildingSettings
{
    private const string Static = "static";
    private const string FurnitureFolder = "furniture";
    private const string ContainerFolder = "container";

    // The longest side of a plot, in tiles of the building grid (BuildingGrid.TileCentimetres, 1.83 m).
    public int MostTiles { get; set; } = 64;

    // How high a piece may reach above a new plot's ground, in centimetres, until the staff set another.
    public float PlotHeight { get; set; } = 1500f;

    public int PlotPrice { get; set; } = 5000;

    public int PlotWeeklyTax { get; set; } = 100;

    // The game's form whose mesh stands for a plot's sign: "sign02" (0x000255D7), a blank wooden sign.
    public string SignAsset { get; set; } = "/Game/Forms/worldobjects/activator/sign02.sign02";

    // The catalogue of the architect's drawer. A server that lists its own replaces this one whole.
    public List<PieceSettings> Pieces { get; set; } =
    [
        Piece("house.farmhouse-1", "Farmhouse", PieceCategory.Houses, "FarmHouse01", PieceUse.Residential),
        Piece("house.farmhouse-2", "Farmhouse, long", PieceCategory.Houses, "FarmHouse02", PieceUse.Residential),
        Piece("house.farmhouse-3", "Farmhouse, two floors", PieceCategory.Houses, "Farmhouse03", PieceUse.Residential),
        Piece("house.lower-1", "Town house", PieceCategory.Houses, "HouseLower01", PieceUse.Residential),
        Piece("house.lower-2", "Town house, narrow", PieceCategory.Houses, "HouseLower02", PieceUse.Residential),
        Piece("house.tent-1", "Tent", PieceCategory.Houses, "Tent01", PieceUse.Any),
        Piece("house.tent-2", "Tent, large", PieceCategory.Houses, "Tent02", PieceUse.Any),
        Piece("house.tent-3", "Tent, open", PieceCategory.Houses, "Tent03", PieceUse.Any),
        Piece("house.lower-3", "Town house, wide", PieceCategory.Houses, "HouseLower03", PieceUse.Residential),
        Piece("house.anvil-lower-1", "Coastal cottage", PieceCategory.Houses, "AnvilLCHouse01", PieceUse.Residential),
        Piece("house.anvil-lower-2", "Coastal cottage, long", PieceCategory.Houses, "AnvilLCHouse02", PieceUse.Residential),
        Piece("house.anvil-middle-1", "Coastal town house", PieceCategory.Houses, "AnvilHouseMC01", PieceUse.Residential),
        Piece("house.frame-1", "House frame", PieceCategory.Houses, "FarmHouseConstruction01", PieceUse.Any),
        Piece("house.frame-3", "House frame, roofed", PieceCategory.Houses, "FarmHouseConstruction03", PieceUse.Any),
        Piece("house.inn", "Inn", PieceCategory.Houses, "AnvilLCTavern01", PieceUse.Industrial),
        Piece("floor.brick-large", "Brick floor, large", PieceCategory.Floors, "IDFloor4WayNoCol01A", PieceUse.Any),
        Piece("floor.boardwalk", "Boardwalk", PieceCategory.Floors, "AnvilBoardwalk01", PieceUse.Any),
        Piece("floor.boardwalk-corner", "Boardwalk corner", PieceCategory.Floors, "AnvilBoardwalkCorner01", PieceUse.Any),
        Piece("floor.boardwalk-end", "Boardwalk end", PieceCategory.Floors, "AnvilBoardwalkEnd02", PieceUse.Any),
        Piece("floor.boardwalk-steps", "Boardwalk steps", PieceCategory.Floors, "AnvilBoardwalkStair01", PieceUse.Any),
        Piece("farm.stable", "Stable", PieceCategory.Farm, "FarmStable01", PieceUse.Industrial),
        Piece("farm.stable-town", "Town stable", PieceCategory.Farm, "LowerClassStable01", PieceUse.Industrial),
        Piece("farm.hay", "Hay bale", PieceCategory.Farm, "HayBail01", PieceUse.Any),
        Piece("farm.trough", "Trough", PieceCategory.Farm, "Trough01", PieceUse.Any),
        Piece("farm.well", "Well", PieceCategory.Farm, "CheydinhalWell01", PieceUse.Any),
        Piece("farm.forge", "Forge", PieceCategory.Farm, "Forge01", PieceUse.Industrial),
        Piece("farm.anvil", "Anvil", PieceCategory.Farm, "Anvil01", PieceUse.Industrial),
        Piece("farm.stable-coastal", "Coastal stable", PieceCategory.Farm, "AnvilLCStable01", PieceUse.Industrial),
        Piece("farm.well-stone", "Stone well", PieceCategory.Farm, "AnvilWell01", PieceUse.Any),
        Piece("farm.log-pile", "Log pile", PieceCategory.Farm, "FireLogPile01", PieceUse.Any),
        Piece("farm.barrel-huge", "Great barrel", PieceCategory.Farm, "BarrelHuge01", PieceUse.Industrial),
        Piece("farm.fire-pit", "Fire pit", PieceCategory.Farm, "FirePitFrame", PieceUse.Any),
        Piece("farm.dock", "Wooden dock", PieceCategory.Farm, "AnvilDock01", PieceUse.Industrial),
        Piece("farm.platform", "Wooden platform", PieceCategory.Farm, "AnvilPlatform01", PieceUse.Any),
        Piece("fence.rail", "Rail fence", PieceCategory.Fences, "FenceLent01", PieceUse.Any),
        Piece("fence.rail-three", "Rail fence, three rails", PieceCategory.Fences, "FenceLentTri01", PieceUse.Any),
        Piece("fence.rail-end", "Rail fence end post", PieceCategory.Fences, "FencePostTriEnd", PieceUse.Any),
        Piece("fence.rail-corner", "Rail fence corner post", PieceCategory.Fences, "FencePostTriCorner", PieceUse.Any),
        Piece("fence.rickety", "Rickety fence", PieceCategory.Fences, "RicketyFence01", PieceUse.Any),
        Piece("fence.middle", "Boarded fence, tall", PieceCategory.Fences, "FenceMid01", PieceUse.Any),
        Piece("fence.end", "Boarded fence end, tall", PieceCategory.Fences, "FenceEnd02", PieceUse.Any),
        Piece("fence.post", "Boarded fence post, tall", PieceCategory.Fences, "FencePost01", PieceUse.Any),
        Piece("fence.wood-post", "Wooden post", PieceCategory.Fences, "WoodPost01", PieceUse.Any),
        Piece("fence.corner", "Fence corner", PieceCategory.Fences, "CornerFenceFull", PieceUse.Any),
        Piece("fence.corner-low", "Fence corner, low", PieceCategory.Fences, "CornerFenceSmall", PieceUse.Any),
        Piece("fence.low", "Boarded fence, low", PieceCategory.Fences, "FenceMid01small", PieceUse.Any),
        Piece("fence.low-end", "Boarded fence end, low", PieceCategory.Fences, "FenceEnd01small", PieceUse.Any),
        Piece("fence.gate", "Fence gate", PieceCategory.Fences, "GateFence", PieceUse.Any),
        Object("furniture.bed", "Bed", FurnitureFolder, "LCBed01R", PieceObject.Furniture, "0x0002F3F3"),
        Object("furniture.bed-double", "Double bed", FurnitureFolder, "MCDoubleBed01LR", PieceObject.Furniture, "0x0002ED01"),
        Object("furniture.chair", "Chair", FurnitureFolder, "LCChair01F", PieceObject.Furniture, "0x0002F18A"),
        Object("furniture.stool", "Stool", FurnitureFolder, "LCStool01F", PieceObject.Furniture, "0x0002F19F"),
        Object("furniture.bench", "Bench", FurnitureFolder, "LCBench01Fall", PieceObject.Furniture, "0x0001FD77"),
        Object("furniture.bed-plain-double", "Double bed, plain", FurnitureFolder, "LCBed03LR", PieceObject.Furniture, "0x0002F183"),
        Object("furniture.bed-town", "Town bed", FurnitureFolder, "MCSingleBed01R", PieceObject.Furniture, "0x0002ED06"),
        Object("furniture.bed-fine", "Fine bed", FurnitureFolder, "UCSingleBed01R", PieceObject.Furniture, "0x0002EEAB"),
        Object("furniture.bed-fine-double", "Fine double bed", FurnitureFolder, "UCDoubleBed01LR", PieceObject.Furniture, "0x0002EEB8"),
        Object("furniture.chair-town", "Town chair", FurnitureFolder, "MCChair01F", PieceObject.Furniture, "0x0002F1AD"),
        Object("furniture.chair-fine", "Fine chair", FurnitureFolder, "UCChair01F", PieceObject.Furniture, "0x0002ED35"),
        Object("furniture.stool-town", "Town stool", FurnitureFolder, "MCStool01F", PieceObject.Furniture, "0x0002F1AF"),
        Object("furniture.bench-town", "Town bench", FurnitureFolder, "MCBench02F", PieceObject.Furniture, "0x0002ECE7"),
        Object("furniture.bench-fine", "Fine bench", FurnitureFolder, "UCBench01F", PieceObject.Furniture, "0x0002F1E0"),
        Piece("furniture.table-2", "Table, long", PieceCategory.Furniture, "LowerClasstable02", PieceUse.Any),
        Piece("furniture.bar", "Bar counter", PieceCategory.Furniture, "LowerClassBar01", PieceUse.Any),
        Piece("furniture.fireplace", "Fireplace", PieceCategory.Furniture, "Fireplace01", PieceUse.Any),
        Piece("furniture.table", "Table", PieceCategory.Furniture, "LowerClasstable01", PieceUse.Any),
        Object("furniture.chest", "Chest", ContainerFolder, "ChestClutterLower01Empty", PieceObject.Container, "0x000086C1"),
        Object("furniture.cupboard", "Cupboard", ContainerFolder, "CupboardClothesMiddleEmpty", PieceObject.Container, "0x000086C3"),
        Object("furniture.drawers", "Chest of drawers", ContainerFolder, "DrawerClutterLowerEmpty", PieceObject.Container, "0x000086BD"),
        Object("furniture.chest-town", "Town chest", ContainerFolder, "ChestClutterMiddle01Empty", PieceObject.Container, "0x000086C4"),
        Object("furniture.chest-fine", "Fine chest", ContainerFolder, "ChestClutterUpper02Empty", PieceObject.Container, "0x000086C5"),
        Object("furniture.cupboard-fine", "Fine cupboard", ContainerFolder, "CupboardClutterUpperEmpty", PieceObject.Container, "0x000C5532"),
        Object("furniture.desk", "Desk", ContainerFolder, "DeskClutterUpperEmpty", PieceObject.Container, "0x000C5531"),
        Object("furniture.wardrobe", "Wardrobe", ContainerFolder, "DrawerClutterUpperClothing01Empty", PieceObject.Container, "0x000086BE"),
        Piece("furniture.shelves", "Shelves", PieceCategory.Furniture, "LowerClassShelves01", PieceUse.Any),
        Piece("decoration.barrel", "Barrel", PieceCategory.Decoration, "LowerClassBarrel01", PieceUse.Any),
        Piece("decoration.rug", "Rug", PieceCategory.Decoration, "LowerClassRug01", PieceUse.Any),
        Piece("decoration.rug-2", "Rug, striped", PieceCategory.Decoration, "LowerClassRug02", PieceUse.Any),
        Piece("decoration.rug-coastal", "Coastal rug", PieceCategory.Decoration, "AnvilRugMid01", PieceUse.Any),
        Piece("decoration.painting", "Painting", PieceCategory.Decoration, "AnvilPicture01", PieceUse.Any),
        Piece("decoration.curtain", "Curtain", PieceCategory.Decoration, "AnvilCurtain01", PieceUse.Any),
        Piece("decoration.banner", "Banner", PieceCategory.Decoration, "AnvilCastleBanner01NH", PieceUse.Any),
        Piece("decoration.tree-bench", "Bench round a tree", PieceCategory.Decoration, "AnvilTreeBench01", PieceUse.Any),
    ];

    // A server owner's mistake stops the mod at start rather than turning into a rule nobody meant.
    public void Check()
    {
        if (MostTiles < 1 || PlotHeight <= 0f || PlotPrice < 0 || PlotWeeklyTax < 0)
            throw new InvalidOperationException("building in config.json: mostTiles must be 1 or more, plotHeight above 0, plotPrice and plotWeeklyTax 0 or more.");

        if (string.IsNullOrWhiteSpace(SignAsset))
            throw new InvalidOperationException("building in config.json: signAsset must name the game's form or mesh a plot's sign stands as.");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var piece in Pieces)
        {
            if (string.IsNullOrWhiteSpace(piece.Key) || piece.Key.Length > PlotBook.MostPieceLength || !keys.Add(piece.Key))
                throw new InvalidOperationException($"building.pieces in config.json: every key must be set, unique and at most {PlotBook.MostPieceLength} letters ('{piece.Key}').");

            if (string.IsNullOrWhiteSpace(piece.Name) || string.IsNullOrWhiteSpace(piece.Asset) || piece.Scale <= 0f)
                throw new InvalidOperationException($"building.pieces in config.json: '{piece.Key}' needs a name, an asset and a scale above 0.");

            if (!Enum.IsDefined(piece.Category) || !Enum.IsDefined(piece.Use) || !Enum.IsDefined(piece.Object))
                throw new InvalidOperationException($"building.pieces in config.json: '{piece.Key}' has a category, a use or an object outside the list.");

            if ((piece.Object == PieceObject.Mesh) != (piece.ReadForm() == 0))
                throw new InvalidOperationException($"building.pieces in config.json: '{piece.Key}' needs a form in hex as furniture or a container, and none as a mesh.");
        }
    }

    private static PieceSettings Piece(string key, string name, PieceCategory category, string form, PieceUse use) =>
        new() { Key = key, Name = name, Category = category, Asset = AssetOf(Static, form), Scale = 1f, Use = use };

    private static PieceSettings Object(string key, string name, string folder, string form, PieceObject kind, string id) =>
        new()
        {
            Key = key,
            Name = name,
            Category = PieceCategory.Furniture,
            Asset = AssetOf(folder, form),
            Scale = 1f,
            Use = PieceUse.Any,
            Object = kind,
            Form = id,
        };

    private static string AssetOf(string folder, string form) => $"/Game/Forms/worldobjects/{folder}/{form}.{form}";
}
