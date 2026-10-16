using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Wrenchables;

public class BlockCrateRotatable : BlockCrate
{
    private float finerStepsMultiplier = 0.25f;
    private float amountFullAllowedBlockRotations = 1f;

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        api.Logger.Notification("[Wrenchables] OnBlockInteractStart called. ItemCode: {0}, Ctrl: {1}",
            activeSlot?.Itemstack?.Collectible.Code.ToNonNullString() ?? "null",
            byPlayer.Entity.Controls.CtrlKey);

        if (activeSlot?.Itemstack?.Collectible is ItemTuningWrench && byPlayer.Entity.Controls.CtrlKey) //TODO no AltKey existing as of 1.22.7 hope for devs to implement and then switch to AltKey instead
        {
            BlockEntityCrateRotatable? blockEntityCrate = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCrateRotatable;
            if (blockEntityCrate != null)
            {
                try
                {
                    float finerSteps = WrenchablesModSystem.Config?.FinerStepsMultiplier ?? finerStepsMultiplier;
                    float amountRots = WrenchablesModSystem.Config?.AmountFullAllowedBlockRotations ?? amountFullAllowedBlockRotations;
                    float rotationLimit = GameMath.TWOPI * amountRots;

                    float newBlockAngle = blockEntityCrate.MeshAngle + GameMath.PIHALF * finerSteps; //Step size = 90° * finerStepsMultiplier (in radians), e.g. 0.25 is steps of 22.5°. Compared against rotationLimit below to trigger a phase flip instead of just wrapping back to 0°.

                    switch (blockEntityCrate.RotationPhase)
                    {
                        case CrateRotationPhase.NormalRealigned:
                            blockEntityCrate.RotationPhase = CrateRotationPhase.FlippedSideways;
                            blockEntityCrate.MeshAngle = 0;
                            break;

                        case CrateRotationPhase.FlippedSidewaysRealigned:
                            blockEntityCrate.RotationPhase = CrateRotationPhase.Normal;
                            blockEntityCrate.MeshAngle = 0;
                            break;

                        default:
                            if (newBlockAngle >= rotationLimit)
                            {
                                blockEntityCrate.MeshAngle = 0;
                                blockEntityCrate.RotationPhase = blockEntityCrate.RotationPhase == CrateRotationPhase.Normal ? CrateRotationPhase.NormalRealigned : CrateRotationPhase.FlippedSidewaysRealigned;
                            }
                            else
                            {
                                blockEntityCrate.MeshAngle = newBlockAngle;
                            }
                            break;
                    }

                    blockEntityCrate.MarkDirty(true);

                    api.Logger.Notification("[Wrenchables] Rotation applied. New MeshAngle: {0}, RotationState: {1}",
                        blockEntityCrate.MeshAngle, blockEntityCrate.RotationPhase);

                }
                catch (Exception ex)
                {

                    api.Logger.Error("[Wrenchables] Error while rotating the crate: {0}", ex);
                }
            }
            else
            {
                api.Logger.Warning("[Wrenchables] Cast to BlockEntityCrateRotatable failed - blockEntityCrate is null.");
            }
            return true;
        }
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    private static readonly Cuboidf[] FullCubeCollisionBox = new Cuboidf[]
    {
        new Cuboidf(0, 0, 0, 1, 1, 1)
    };

    /// <summary>
    /// Returns a simplified full-cube collision box whenever the crate is closed or tipped over, so a rotated/tipped crate doesn't fall back to inconsistent vanilla boxes. Only an open, non-tipped crate keeps the precise vanilla boxes.
    /// </summary>
    /// <param name="blockAccessor">block accessor for the current world</param>
    /// <param name="blockPos">position of the crate block</param>
    /// <returns>full-cube box array if closed or tipped, otherwise the vanilla collision boxes</returns>
    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos blockPos)
    {
        var blockEntityCrate = blockAccessor.GetBlockEntity(blockPos) as BlockEntityCrateRotatable;

        if (blockEntityCrate == null)
        {
            return FullCubeCollisionBox;
        }

        bool isTipped = blockEntityCrate.RotationPhase == CrateRotationPhase.FlippedSideways || blockEntityCrate.RotationPhase == CrateRotationPhase.FlippedSidewaysRealigned || blockEntityCrate.RotationPhase == CrateRotationPhase.FlippedForward || blockEntityCrate.RotationPhase == CrateRotationPhase.FlippedForwardRealigned;

        if (isTipped || blockEntityCrate.LidState == "closed")
        {
            return FullCubeCollisionBox;
        }

        return base.GetCollisionBoxes(blockAccessor, blockPos);
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        Block? vanillaCrate = api.World.GetBlock(new AssetLocation("game:crate"));
        return vanillaCrate?.GetHeldItemName(itemStack) ?? base.GetHeldItemName(itemStack);
    }

    public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
    {
        Block? vanillaCrate = api.World.GetBlock(new AssetLocation("game:crate"));
        return vanillaCrate?.GetPlacedBlockName(world, pos) ?? base.GetPlacedBlockName(world, pos);
    }
}