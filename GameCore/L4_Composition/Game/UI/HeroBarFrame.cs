using System;
using System.Collections.Generic;
using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class HeroBarFrame : UiContainer, IClickable
{
    private const int MaxHeroes = 7;
    private const float CellSize = 48.64f, CellStepY = 65.51f, OriginX = 4.8f, OriginY = 32f;
    private const string IconFallback = @"ReplaceableTextures\CommandButtons\BTNTemp.blp";
    private const string DisabledDir = @"ReplaceableTextures\CommandButtonsDisabled\";

    private readonly struct Slot
    {
        public readonly TextureFrame Icon;
        public readonly StatusBarFrame Hp;
        public readonly StatusBarFrame Mp;

        public Slot(TextureFrame icon, StatusBarFrame hp, StatusBarFrame mp)
        {
            Icon = icon;
            Hp = hp;
            Mp = mp;
        }
    }

    private readonly Slot[] _slots = new Slot[MaxHeroes];
    private readonly Func<string, ITexture> _iconResolver;
    private readonly List<Unit> _heroes = new();
    private int _heroCount;

    public Action<Unit> HeroClicked;

    public HeroBarFrame(IUiTextureResolver textures) : base("HeroBar")
    {
        _iconResolver = textures.Resolve;
        ITexture fillTexture = textures.Resolve(
            @"UI\Feedback\HpBarConsole\human-healthbar-fill.blp");
        for (int index = 0; index < MaxHeroes; index++)
        {
            var icon = new TextureFrame($"HeroIcon{index}");
            var hp = new StatusBarFrame($"HeroHp{index}")
            {
                FillColor = new Vector4(0.1f, 0.78f, 0.12f, 1f),
                BackgroundColor = new Vector4(0.02f, 0.02f, 0.02f, 1f),
                FillTexture = fillTexture,
            };
            var mp = new StatusBarFrame($"HeroMp{index}")
            {
                FillColor = new Vector4(0.05f, 0.2f, 0.9f, 1f),
                BackgroundColor = new Vector4(0.02f, 0.02f, 0.02f, 1f),
                FillTexture = fillTexture,
            };
            _slots[index] = new Slot(icon, hp, mp);
            AddChild(icon);
            AddChild(hp);
            AddChild(mp);
        }
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        for (int index = 0; index < MaxHeroes; index++)
        {
            UiRect iconBounds = IconBounds(index);
            float x = iconBounds.X, y = iconBounds.Y;
            ref Slot slot = ref _slots[index];
            slot.Icon.RenderBounds = iconBounds;
            slot.Hp.RenderBounds = new UiRect(x, y + CellSize, CellSize, 4);
            slot.Mp.RenderBounds = new UiRect(x, y + CellSize + 4, CellSize, 3);
        }
    }

    internal static UiRect IconBounds(int index) => new(OriginX,
        OriginY + Math.Max(0, index) * CellStepY, CellSize, CellSize);

    public void Update(IReadOnlyList<Unit> heroes, Unit selected)
    {
        _heroes.Clear();
        if (heroes != null)
        {
            for (int index = 0; index < heroes.Count; index++)
                _heroes.Add(ShowInHeroBar(heroes[index]) ? heroes[index] : null);
        }
        _heroCount = Math.Min(MaxHeroes, _heroes.Count);

        for (int index = 0; index < MaxHeroes; index++)
        {
            ref Slot slot = ref _slots[index];
            bool active = index < _heroCount && _heroes[index] != null;
            slot.Icon.Visible = active;
            slot.Hp.Visible = active;
            slot.Mp.Visible = active;
            if (!active) continue;

            Unit hero = _heroes[index];
            string icon = string.IsNullOrWhiteSpace(hero.Type?.IconPath)
                ? IconFallback : hero.Type.IconPath;
            if (!hero.Alive) icon = NativeCommandButtonBinding.DisabledArt(icon, DisabledDir);
            slot.Icon.Texture = _iconResolver(icon);
            slot.Icon.Color = !hero.Alive
                ? new Vector4(.72f, .72f, .72f, 1f)
                : ReferenceEquals(hero, selected)
                    ? new Vector4(1f, 1f, 0.65f, 1f)
                    : Vector4.One;

            bool hasHealth = hero.Alive && hero.MaxHp > 0;
            slot.Hp.Visible = hasHealth;
            slot.Hp.Ratio = hasHealth ? Math.Clamp(hero.Hp / hero.MaxHp, 0, 1) : 0;
            bool hasMana = hero.Alive && hero.MaxMana > 0;
            slot.Mp.Visible = hasMana;
            slot.Mp.Ratio = hasMana ? Math.Clamp(hero.Mana / hero.MaxMana, 0, 1) : 0;
        }
    }

    private static bool ShowInHeroBar(Unit unit)
        => unit != null && unit.IsHero && (unit.Type == null || !unit.Type.HideInHeroBar);

    bool IClickable.Enabled => Enabled;

    public override UiFrame HitTest(Vector2 point)
    {
        if (!Visible || !Enabled || MouseTransparent) return null;
        for (int index = 0; index < _heroCount; index++)
            if (_heroes[index] != null
                && _slots[index].Icon.RenderBounds.Contains(point.X, point.Y)) return this;
        return null;
    }

    void IClickable.OnPointerDown(Vector2 pos)
    {
        for (int index = 0; index < _heroCount; index++)
        {
            var bounds = new UiRect(OriginX, OriginY + index * CellStepY, CellSize, CellStepY);
            if (_heroes[index] != null && bounds.Contains(pos.X, pos.Y))
            {
                HeroClicked?.Invoke(_heroes[index]);
                return;
            }
        }
    }

    void IClickable.OnPointerUp(Vector2 pos) { }

    void IClickable.OnClick(int button) { }
}