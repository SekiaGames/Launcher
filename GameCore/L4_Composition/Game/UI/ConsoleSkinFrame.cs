using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

/// <summary>
/// Warcraft 控制台底板的手写回退实现（FDF ConsoleUI 不可用时使用）。
/// 数值与 warsmash SmashConsoleUI.fdf 一致：上条 0.032 高、下条 0.176 高、每块 0.256 宽。
/// </summary>
public sealed class ConsoleSkinFrame : UiContainer
{
    const float Tile = 327.68f;
    const float Edge = 40.96f;
    const float BottomHeight = 225.28f;

    readonly TextureFrame[] _bottom = new TextureFrame[4];
    readonly TextureFrame[] _top = new TextureFrame[5];
    readonly TextureFrame _inventoryCover;

    public ConsoleSkinFrame(IUiTextureResolver textures, War3UiSkin skin)
        : base("ConsoleSkin")
    {
        MouseTransparent = true;
        ITexture t1 = textures.Resolve(skin.Get("ConsoleTexture01",
            @"UI\Console\Human\HumanUITile01.blp"));
        ITexture t2 = textures.Resolve(skin.Get("ConsoleTexture02",
            @"UI\Console\Human\HumanUITile02.blp"));
        ITexture t3 = textures.Resolve(skin.Get("ConsoleTexture03",
            @"UI\Console\Human\HumanUITile03.blp"));
        ITexture t4 = textures.Resolve(skin.Get("ConsoleTexture04",
            @"UI\Console\Human\HumanUITile04.blp"));

        AddBottom(0, t1, new UiRect(0, .3125f, 1, .6875f));
        AddBottom(1, t2, new UiRect(0, .4140625f, 1, .5859375f));
        AddBottom(2, t3, new UiRect(0, .3125f, 1, .6875f));
        AddBottom(3, t4, new UiRect(0, .3125f, 1, .6875f));

        AddTop(0, t1, new UiRect(0, 0, 1, .125f));
        AddTop(1, t2, new UiRect(0, 0, .33984375f, .125f));
        AddTop(2, t2, new UiRect(.79296875f, 0, .20703125f, .125f));
        AddTop(3, t3, new UiRect(0, 0, 1, .125f));
        AddTop(4, t4, new UiRect(0, 0, 1, .125f));

        _inventoryCover = new TextureFrame("ConsoleInventoryCover")
        {
            Texture = textures.Resolve(skin.Get("ConsoleInventoryCoverTexture",
                @"UI\Console\Human\HumanUITile-InventoryCover.blp")),
            UV = new UiRect(0, .3125f, 1, .6875f),
        };
        AddChild(_inventoryCover);
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        const float x = 0f;
        float bottom = viewportHeight - BottomHeight;

        _bottom[0].RenderBounds = new UiRect(x, bottom, Tile, BottomHeight);
        _bottom[1].RenderBounds = new UiRect(x + Tile, viewportHeight - 192f, Tile, 192f);
        _bottom[2].RenderBounds = new UiRect(x + Tile * 2, bottom, Tile, BottomHeight);
        _bottom[3].RenderBounds = new UiRect(x + Tile * 3, bottom, Edge, BottomHeight);

        _top[0].RenderBounds = new UiRect(x, 0, Tile, Edge);
        _top[1].RenderBounds = new UiRect(x + Tile, 0, 111.36f, Edge);
        _top[2].RenderBounds = new UiRect(x + 587.52f, 0, 67.84f, Edge);
        _top[3].RenderBounds = new UiRect(x + 655.36f, 0, Tile, Edge);
        _top[4].RenderBounds = new UiRect(x + 983.04f, 0, Edge, Edge);

        _inventoryCover.RenderBounds =
            new UiRect(x + 604.16f, bottom, 163.84f, BottomHeight);
    }

    public void SetInventoryVisible(bool visible) => _inventoryCover.Visible = !visible;

    void AddBottom(int index, ITexture texture, UiRect uv)
    {
        _bottom[index] = new TextureFrame($"ConsoleBottom{index}") { Texture = texture, UV = uv };
        AddChild(_bottom[index]);
    }

    void AddTop(int index, ITexture texture, UiRect uv)
    {
        _top[index] = new TextureFrame($"ConsoleTop{index}") { Texture = texture, UV = uv };
        AddChild(_top[index]);
    }
}
