using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Wrenchables;

public enum CrateRotationPhase : byte
{
    Normal = 0,
    NormalRealigned = 1,
    FlippedSideways = 2,
    FlippedSidewaysRealigned = 3,
    FlippedForward = 4,
    FlippedForwardRealigned = 5
}

public class BlockEntityCrateRotatable : BlockEntityCrate
{
    private MeshData? ownMeshRotated;
    private float lastCachedMeshAngle = -1;

    public CrateRotationPhase RotationPhase = CrateRotationPhase.Normal;
    private CrateRotationPhase lastCachedRotationPhase = (CrateRotationPhase)255;
    private string? lastCachedLidState;
    private int lastCachedContentStackHash;
    private int lastCachedContentStackSize;

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt("rotationPhase", (int)RotationPhase);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        RotationPhase = (CrateRotationPhase)tree.GetInt("rotationPhase", 0);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        var block = Block as BlockCrate;
        if (block == null) { return base.OnTesselation(mesher, tesselator); }

        var contentStack = Inventory.FirstNonEmptySlot?.Itemstack;
        int contentStackHash = contentStack?.GetHashCode() ?? 0;
        int contentStackSize = contentStack?.StackSize ?? 0;


        if (ownMeshRotated == null
            || lastCachedMeshAngle != MeshAngle
            || lastCachedRotationPhase != RotationPhase
            || lastCachedLidState != LidState
            || lastCachedContentStackHash != contentStackHash
            || lastCachedContentStackSize != contentStackSize)
        {
            var cshape = block.Props[type].Shape;

            bool isFlippedSideways = RotationPhase == CrateRotationPhase.FlippedSideways || RotationPhase == CrateRotationPhase.FlippedSidewaysRealigned;
            bool isFlippedForward = RotationPhase == CrateRotationPhase.FlippedForward || RotationPhase == CrateRotationPhase.FlippedForwardRealigned;

            float extraX = isFlippedForward ? GameMath.PIHALF : 0;
            float extraZ = isFlippedSideways ? GameMath.PIHALF : 0;

            var shellMesh = GenerateOuterShell(block, cshape, extraX, extraZ);

            if (contentStack != null && LidState != "closed")
            {
                AddContentToShell(contentStack, extraX, extraZ, shellMesh);
            }

            ownMeshRotated = shellMesh;
            lastCachedMeshAngle = MeshAngle;
            lastCachedRotationPhase = RotationPhase;
            lastCachedLidState = LidState;
            lastCachedContentStackHash = contentStackHash;
            lastCachedContentStackSize = contentStackSize;
        }

        mesher.AddMeshData(ownMeshRotated);
        return true;
    }

    private void AddContentToShell(ItemStack? contentStack, float extraX, float extraZ, MeshData shellMesh)
    {
        var contentMesh = GenContentMeshFixed((ICoreClientAPI)Api, contentStack, null);
        if (contentMesh != null)
        {
            bool contentFollowsFlip = ContentShouldFollowFlip(contentStack);
            float contentExtraX = contentFollowsFlip ? extraX : 0;
            float contentExtraZ = contentFollowsFlip ? extraZ : 0;

            contentMesh = contentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), contentExtraX, MeshAngle, contentExtraZ);
            shellMesh.AddMeshData(contentMesh);
        }
    }

    private MeshData GenerateOuterShell(BlockCrate block, CompositeShape cshape, float extraX, float extraZ)
    {
        var shellMesh = block.GenMesh((ICoreClientAPI)Api, null, type, label, LidState, cshape);
        shellMesh = shellMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), extraX, MeshAngle, extraZ);

        return shellMesh;
    }

    private bool ContentShouldFollowFlip(ItemStack? contentStack)
    {
        //TODO hard coded list, try using via item attributes later
        string path = contentStack?.Collectible?.Code?.Path ?? "";
        return path.Contains("logpiece") || path.Contains("log-piece") || path.Contains("log"); //TODO might be just "log"
    }

    private MeshData? GenContentMeshFixed(ICoreClientAPI capi, ItemStack? contentStack, Vec3f? rotation)
    {
        if (contentStack == null) return null;

        var groundStorable = contentStack.Collectible?.GetBehavior<CollectibleBehaviorGroundStorable>();
        var stackingModel = groundStorable?.StorageProps?.StackingModel;
        capi.Logger.Debug("[Wrenchables] Building content mesh for {0}, stackingmodel: {1}, stack size: {2}", contentStack.Collectible?.Code, stackingModel?.ToString() ?? "none", contentStack.StackSize);

        ITexPositionSource? contentSource;
        float fillHeight;

        if (stackingModel != null)
        {
            contentSource = new CollectibleTexPositionSource(capi, contentStack);
            fillHeight = 0; //TODO fill this according to crate fill percentage, maybe in extracted method 
        }
        else
        {
            contentSource = BlockBarrel.getContentTexture(capi, contentStack, out fillHeight);
        }
        if (contentSource == null) return null;

        var inCrateShapeAttr = contentStack.ItemAttributes?["inCrateShape"];
        AssetLocation loc;

        if (stackingModel != null)
        {
            loc = stackingModel.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
        }
        else if (inCrateShapeAttr != null && inCrateShapeAttr.Exists)
        {
            loc = AssetLocation.Create(inCrateShapeAttr.AsString(), contentStack.Collectible?.Code?.Domain ?? "game").WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
        }
        else
        {
            loc = AssetLocation.Create("shapes/block/wood/crate/contents.json", "game");
        }

        Shape? shape = Shape.TryGet(capi, loc);
        if (shape == null) return null;


        if (stackingModel != null)
        {
            RemoveEmptyStackElementsFromShape(shape, contentStack);
        }

        capi.Tesselator.TesselateShape("cratecontents", shape, out MeshData contentMesh, contentSource, rotation);
        if (contentMesh == null) return null;

        if (stackingModel != null)
        {
            contentMesh = ScaleMeshUniform(contentMesh, 0.80f);
        }
        contentMesh.Translate(0, fillHeight * 1.1f, 0);
        return contentMesh;
    }

    private static MeshData ScaleMeshUniform(MeshData contentMesh, float uniformScaleFactor)
    {
        contentMesh = contentMesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), uniformScaleFactor, uniformScaleFactor, uniformScaleFactor);
        contentMesh = contentMesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, GameMath.PIHALF, 0f);

        return contentMesh;
    }

    private static void RemoveEmptyStackLayersFromShape(Shape shape, ItemStack contentStack) //TODO remove if generic variant (method RemoveEmptyStackElementsFromShape below) works
    {
        int maxStackSize = contentStack.Collectible.MaxStackSize;
        int stackSize = contentStack.StackSize;
        if (maxStackSize <= 0 || stackSize <= 0) return;

        const int totalLogs = 16;
        int visibleLogs = GameMath.Clamp((int)System.Math.Ceiling(totalLogs * (float)stackSize / maxStackSize), 1, totalLogs);

        int logIndex = totalLogs;
        for (int layer = 4; layer >= 1; layer--)
        {
            for (int i = 3; i >= 0; i--)
            {
                if (logIndex > visibleLogs)
                {
                    shape.RemoveElementByName($"Log{layer}-{i}");
                }
                logIndex--;
            }
        }
    }

    private static void RemoveEmptyStackElementsFromShape(Shape shape, ItemStack contentStack)
    {
        int maxStackSize = contentStack.Collectible.MaxStackSize;
        int stackSize = contentStack.StackSize;
        if (maxStackSize <= 0 || stackSize <= 0 || shape.Elements == null) return;

        int totalElements = shape.Elements.Length;
        int visibleElements = GameMath.Clamp((int)System.Math.Ceiling(totalElements * (float)stackSize / maxStackSize), 1, totalElements);

        for (int i = totalElements - 1; i >= visibleElements; i--)
        {
            shape.RemoveElementByName(shape.Elements[i].Name);
        }
    }
}