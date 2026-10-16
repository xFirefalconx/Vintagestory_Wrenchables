using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;


namespace Wrenchables;

public class WrenchablesModSystem : ModSystem
{
    public static WrenchablesConfig Config = new WrenchablesConfig();

    public static string ConfigName = "wrenchables-config.json";

    private FastVec2f finerStepsLimits = new FastVec2f(0.01f, 2f);
    private FastVec2f amountRotsLimits = new FastVec2f(0.125f, 10f);
    private int configRewrites = 0;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        Config = api.LoadModConfig<WrenchablesConfig>(ConfigName);
        if (Config == null)
        {
            Config = new WrenchablesConfig();
            api.StoreModConfig(Config, ConfigName);
        }

        configRewrites = ClampConfigValues(api);
        if (configRewrites > 0)
        {
            api.StoreModConfig(Config, ConfigName);
            api.Logger.Warning($"[Wrenchables] {configRewrites} config values reset to default.");
            configRewrites = 0;
        }

        api.RegisterBlockClass(Mod.Info.ModID + ".BlockCrateRotatable", typeof(BlockCrateRotatable));
        api.RegisterItemClass(Mod.Info.ModID + ".ItemTuningWrench", typeof(ItemTuningWrench));
        api.RegisterBlockEntityClass(Mod.Info.ModID + ".BlockEntityCrateRotatable", typeof(BlockEntityCrateRotatable));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        api.Logger.Debug("[Wrenchables] StartClientSide reached, registering commands now.");
        CheckHeldItemViaCommand(api);
        api.Logger.Debug("[Wrenchables] Command registration call completed.");
    }

    private static void CheckHeldItemViaCommand(ICoreClientAPI api)
    {
        api.ChatCommands.Create("checkitem")
                        .WithDescription("Displays displayInsideCrate attribute of held item")
                        .RequiresPrivilege(Privilege.chat)
                        .HandleWith(args =>
                        {
                            var slot = api.World.Player.InventoryManager.ActiveHotbarSlot;
                            var stack = slot?.Itemstack;
                            if (stack == null) return TextCommandResult.Success("Empty hand.");

                            var attr = stack.ItemAttributes?["displayInsideCrate"];
                            string value = attr != null && attr.Exists ? attr.AsBool(true).ToString() : "not set";

                            return TextCommandResult.Success($"{stack.Collectible.Code}: displayInsideCrate = {value}");
                        });
    }


    private int ClampConfigValues(ICoreAPI api)
    {
        if (Config.FinerStepsMultiplier < finerStepsLimits.A || Config.FinerStepsMultiplier > finerStepsLimits.B)
        {
            Config.FinerStepsMultiplier = GameMath.Clamp(Config.FinerStepsMultiplier, finerStepsLimits.A, finerStepsLimits.B);
            api.Logger.Warning("[Wrenchables] FinerStepsMultiplier clamped, nonsense values found in config file.");
            configRewrites += 1;
        }
        if (Config.AmountFullAllowedBlockRotations < amountRotsLimits.A || Config.AmountFullAllowedBlockRotations > amountRotsLimits.B)
        {
            Config.AmountFullAllowedBlockRotations = GameMath.Clamp(Config.AmountFullAllowedBlockRotations, amountRotsLimits.A, amountRotsLimits.B);
            api.Logger.Warning("[Wrenchables] AmountFullAllowedBlockRotations clamped, nonsense values found in config file.");
            configRewrites += 1;
        }

        return configRewrites;
    }

}
