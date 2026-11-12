using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game.UI;

/// <summary>
/// 左侧英雄头像栏（客户端 HeroBarFrame）：竖排最多 7 个己方英雄，
/// 图标下压血条蓝条；点一下选中该英雄，再点一次把镜头对过去。
/// </summary>
public sealed class HeroBarFrame
{
    private const int MaxSlots = 7;
    private const float SlotSize = 44f;
    private const float SlotStep = 58f;
    private const float OriginX = 8f;
    private const float OriginY = 56f;

    private readonly List<Button> _icons = new();
    private readonly List<ColorRect> _hpFill = new();
    private readonly List<ColorRect> _mpFill = new();
    private readonly List<Unit> _heroes = new();
    private readonly string[] _iconKeys = new string[MaxSlots];

    public Action<Unit> HeroClicked;

    public HeroBarFrame(Node parent)
    {
        var layer = new CanvasLayer { Name = "HeroBar", Layer = 2 };
        parent.AddChild(layer);

        for (int index = 0; index < MaxSlots; index++)
        {
            int slot = index;
            float y = OriginY + index * SlotStep;

            var button = new Button
            {
                Name = $"HeroIcon{index}",
                Position = new Vector2(OriginX, y),
                Size = new Vector2(SlotSize, SlotSize),
                Visible = false,
                ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Center,
                Flat = false,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            button.AddThemeStyleboxOverride("normal", FrameStyle(new Color(0.13f, 0.12f, 0.1f)));
            button.AddThemeStyleboxOverride("hover", FrameStyle(new Color(0.24f, 0.21f, 0.14f)));
            button.AddThemeStyleboxOverride("pressed", FrameStyle(new Color(0.32f, 0.27f, 0.16f)));
            button.Pressed += () =>
            {
                if (slot < _heroes.Count && _heroes[slot] != null) HeroClicked?.Invoke(_heroes[slot]);
            };
            layer.AddChild(button);
            _icons.Add(button);

            AddBar(layer, new Vector2(OriginX, y + SlotSize), new Color(0.02f, 0.02f, 0.02f, 1f));
            _hpFill.Add(AddBar(layer, new Vector2(OriginX, y + SlotSize), new Color(0.1f, 0.78f, 0.12f)));

            AddBar(layer, new Vector2(OriginX, y + SlotSize + 5f), new Color(0.02f, 0.02f, 0.02f, 1f));
            _mpFill.Add(AddBar(layer, new Vector2(OriginX, y + SlotSize + 5f), new Color(0.05f, 0.2f, 0.9f)));
        }
    }

    private static ColorRect AddBar(Node layer, Vector2 position, Color color)
    {
        var bar = new ColorRect
        {
            Color = color,
            Position = position,
            Size = new Vector2(SlotSize, 4f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(bar);
        return bar;
    }

    private static StyleBoxFlat FrameStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = new Color(0.52f, 0.44f, 0.25f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
    }

    public void Update(IReadOnlyList<Unit> heroes, Unit selected)
    {
        _heroes.Clear();
        if (heroes != null)
        {
            foreach (Unit hero in heroes)
            {
                if (hero == null || hero.Type == null || !hero.IsHero) continue;
                if (hero.Type.HideInHeroBar) continue;
                _heroes.Add(hero);
                if (_heroes.Count >= MaxSlots) break;
            }
        }

        for (int index = 0; index < MaxSlots; index++)
        {
            bool show = index < _heroes.Count;
            _icons[index].Visible = show;
            _hpFill[index].Visible = show;
            _mpFill[index].Visible = show;
            if (!show) continue;

            Unit hero = _heroes[index];
            UnitType type = hero.Type;
            string iconKey = type?.Id ?? "";
            if (!string.Equals(_iconKeys[index], iconKey, StringComparison.Ordinal))
            {
                _iconKeys[index] = iconKey;
                _icons[index].Icon = type != null && !string.IsNullOrEmpty(type.IconPath)
                    ? Assets.LoadTexture(type.IconPath)
                    : null;
            }

            bool chosen = selected != null && ReferenceEquals(selected, hero);
            _icons[index].Modulate = !hero.Alive
                ? new Color(0.55f, 0.55f, 0.55f)
                : chosen
                    ? new Color(1f, 0.97f, 0.62f)
                    : Colors.White;

            float hp = hero.Alive && hero.MaxHp > 0f ? Mathf.Clamp(hero.Hp / hero.MaxHp, 0f, 1f) : 0f;
            _hpFill[index].Size = new Vector2(SlotSize * hp, 4f);
            float mp = hero.Alive && hero.MaxMana > 0f ? Mathf.Clamp(hero.Mana / hero.MaxMana, 0f, 1f) : 0f;
            _mpFill[index].Size = new Vector2(SlotSize * mp, 3f);
        }
    }
}