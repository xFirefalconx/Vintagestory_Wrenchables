using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Wrenchables;
internal class CollectibleTexPositionSource : ITexPositionSource
{
    private readonly ICoreClientAPI capi;
    private readonly ItemStack stack;

    public CollectibleTexPositionSource(ICoreClientAPI capi, ItemStack stack)
    {
        this.capi = capi;
        this.stack = stack;
    }
    public Size2i? AtlasSize => capi.BlockTextureAtlas.Size;

    public TextureAtlasPosition this[string textureCode]
    {
        get
        {
            var textures = stack.Class == EnumItemClass.Item ? stack.Item?.Textures : stack.Block?.Textures;
            if (textures != null && textures.TryGetValue(textureCode, out var requestedTexture))
            {
                TextureAtlasPosition? requestedPosition = FindAtlasPosition(requestedTexture, textureCode);
                if (requestedPosition != null) return requestedPosition;
            }
            if (textures != null && textures.TryGetValue("south", out var fallbackTexture))
            {
                TextureAtlasPosition? fallbackPosition = FindAtlasPosition(fallbackTexture, "south (fallback for " + textureCode + ")");
                if (fallbackPosition != null) return fallbackPosition;
            }

            capi.Logger.Warning("[Wrenchables] No texture for code '{0}' on {1}. Available codes: {2}", textureCode, stack.Collectible?.Code, textures == null ? "none" : string.Join(", ", textures.Keys));
            return capi.BlockTextureAtlas.UnknownTexturePosition;
        }
    }

    private TextureAtlasPosition? FindAtlasPosition(CompositeTexture compositeTexture, string lookupLabel)
    {
        if (compositeTexture.Baked == null)
        {
            capi.Logger.Warning("[Wrenchables] Texture '{0}' of {1} is not baked.", lookupLabel, stack.Collectible?.Code);
            return null;
        }

        TextureAtlasPosition? position = capi.BlockTextureAtlas[compositeTexture.Baked.BakedName];
        if (position == null)
        {
            capi.Logger.Warning("[Wrenchables] Texture '{0}' of {1} (baked name '{2}') is not in the block atlas.", lookupLabel, stack.Collectible?.Code, compositeTexture.Baked.BakedName);
        }
        return position;
    }

}