using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Wrenchables;

public class ItemTuningWrench : Item
{
    private const string VanillaMetalLangKeyPrefix = "game:material-";

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        HeldPriorityInteract = true;
        api.Logger.Debug("[Wrenchables] ItemTuningWrench OnLoaded, HeldPriorityInteract set to {0}", HeldPriorityInteract);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        IWorldAccessor world = byEntity.World;

        Block? swapTarget = null;
        if (blockSel != null && byEntity.Controls.CtrlKey)
        {
            Block clickedBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            api.Logger.Debug("[Wrenchables] Clicked block code: {0}", clickedBlock.Code);
            swapTarget = GetSwapTarget(world, clickedBlock);
        }


        if (blockSel == null || swapTarget == null)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        if (!byEntity.Controls.ShiftKey)
        {
            return;
        }

        ReplaceBlockWithEntityData(blockSel, world, swapTarget);

        handling = EnumHandHandling.PreventDefault;
        api.Logger.Debug("[Wrenchables] Block exchanged to {0} and entity recreated successfully.", swapTarget.Code);
    }

    private static Block? GetSwapTarget(IWorldAccessor world, Block clickedBlock)
    {
        AssetLocation vanillaCrateCode = new AssetLocation("game:crate");
        AssetLocation rotatableCrateCode = new AssetLocation("wrenchablesfirefalcon:rotatablecrate");

        if (clickedBlock.Code.Equals(vanillaCrateCode))
        {
            return world.GetBlock(rotatableCrateCode);
        }

        if (clickedBlock.Code.Equals(rotatableCrateCode))
        {
            return world.GetBlock(vanillaCrateCode);
        }
        return null;
    }


    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return new WorldInteraction[]
        {
            new WorldInteraction
            {
                ActionLangCode = "wrenchablesfirefalcon:heldhelp-swapcrate",
                HotKeyCodes = new string[] {"ctrl", "shift"},
                MouseButton = EnumMouseButton.Right
            },
            new WorldInteraction
            {
                ActionLangCode = "wrenchablesfirefalcon:heldhelp-rotatecrate",
                HotKeyCodes = new string[] {"ctrl"},
                MouseButton = EnumMouseButton.Right
            }
        };
    }

    /// <summary>
    /// Builds the displayed name from the translated metal name and the mod's own item name, for example "Wrenchables' Copper Tuning Wrench". Falls back to the default name if the metal variant is missing.
    /// </summary>
    /// <param name="itemStack">item stack the name is requested for</param>
    /// <returns>translated display name of the tuning wrench</returns>
    public override string GetHeldItemName(ItemStack itemStack)
    {
        string? metal = Variant?["metal"];
        if (metal == null)
        {
            return base.GetHeldItemName(itemStack);
        }

        string metalLangKey = VanillaMetalLangKeyPrefix + metal;
        string metalName = Lang.HasTranslation(metalLangKey) ? Lang.Get(metalLangKey) : metal;

        return Lang.GetMatching(Code.Domain + ":item-" + Code.Path, metalName);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        float stepDegrees = 90f * WrenchablesModSystem.Config.FinerStepsMultiplier;
        float fullTurns = WrenchablesModSystem.Config.AmountFullAllowedBlockRotations;

        bool isWholeNumber = fullTurns % 1f == 0f;

        string turnWord = Lang.Get(fullTurns == 1f
            ? "wrenchablesfirefalcon:tuningwrench-desc-singular"
            : "wrenchablesfirefalcon:tuningwrench-desc-plural");

        string fullWord = isWholeNumber ? Lang.Get("wrenchablesfirefalcon:tuningwrench-desc-whole") : "";

        dsc.AppendLine(Lang.Get("wrenchablesfirefalcon:tuningwrench-desc", stepDegrees, fullTurns, turnWord, fullWord));
    }


    private ITreeAttribute SaveBlockAttributes(BlockSelection blockSel, IWorldAccessor world)
    {
        ITreeAttribute savedTree = new TreeAttribute();
        BlockEntity? oldEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
        oldEntity?.ToTreeAttributes(savedTree);

        return savedTree;
    }

    /// <summary>
    /// ExchangeBlock only updates the Block reference on the existing block entity - it does NOT recreate it with the new entityclass. We force a proper re-creation here so the entity actually becomes BlockEntityCrateRotatable instead of staying the old vanilla one.
    /// </summary>
    /// <param name="blockSel">block selection</param>
    /// <param name="world">game world accessor interface</param>
    /// <param name="target">block to target</param>
    private static void SwitchBlockEntity(BlockSelection blockSel, IWorldAccessor world, Block target)
    {
        world.BlockAccessor.ExchangeBlock(target.Id, blockSel.Position);

        world.BlockAccessor.RemoveBlockEntity(blockSel.Position);
        world.BlockAccessor.SpawnBlockEntity(target.EntityClass, blockSel.Position);
    }


    /// <summary>
    /// Restore the saved data into the freshly spawned entity. RotationPhase is not present in the old tree, so it stays at its default - a freshly wrenched crate should not already be tipped. When swapping back to the vanilla crate, the rotation value is ignored because the vanilla entity does not read it.
    /// </summary>
    /// <param name="blockSel">block selection</param>
    /// <param name="world">game world accessor interface</param>
    /// <param name="savedTree">tree attribute data to read from</param>
    private static void RestoreBlockData(BlockSelection blockSel, IWorldAccessor world, ITreeAttribute savedTree)
    {
        BlockEntity? newEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
        newEntity?.FromTreeAttributes(savedTree, world);
        newEntity?.MarkDirty(true);
    }

    private void ReplaceBlockWithEntityData(BlockSelection blockSel, IWorldAccessor world, Block target)
    {
        ITreeAttribute savedTree = SaveBlockAttributes(blockSel, world);
        SwitchBlockEntity(blockSel, world, target);
        RestoreBlockData(blockSel, world, savedTree);
    }
}