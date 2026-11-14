using System;
using System.Collections.Generic;
using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class SelectionGroupFrame : UiContainer, IClickable
{
    const int Slots = 12;
    const int Cols = 6;
    const float CellSize = 28f * 1.05f * 1.3f * 0.9f * 0.9f;
    const float GapX = 5f, GapY = 5f, HpH = 2.7f, MpH = 2.7f;
    const float HpGap = 1.5f, MpGap = 0.75f;
    const float SelectedScale = 1.25f;
    const float PanelTopFromBottom = 117f;
    const float BoxLeft = 380f + 28f * 0.7f + 28f * 0.15f + 28f * 0.22f
        + 28f * 0.20f - 28f * 0.05f - 28f * 0.08f;

    readonly struct Slot
    {
        public readonly TextureFrame Icon;
        public readonly TextureFrame Border;
        public readonly StatusBarFrame Hp;
        public readonly StatusBarFrame Mp;

        public Slot(TextureFrame icon, TextureFrame border, StatusBarFrame hp, StatusBarFrame mp)
        {
            Icon = icon;
            Border = border;
            Hp = hp;
            Mp = mp;
        }
    }

    readonly Slot[] _slots = new Slot[Slots];
    readonly Func<string, ITexture> _iconResolver;
    readonly List<Unit> _selection = new();
    Unit _selected;
    int _selectedIndex = -1;

    public Action<Unit> OnUnitClick;

    /// <summary>
    /// 单选一个能装人的单位时，面板改显示肚子里的乘客（地洞里的苦工、载具乘客）。
    /// 点乘客图标 = 卸下他；与客户端 SelectionGroupFrame.DisplayedCargo 同口径。
    /// </summary>
    internal static IReadOnlyList<Unit> DisplayedCargo(Unit selected, IReadOnlyList<Unit> selection,
        bool controllable)
    {
        if (selection?.Count != 1 || selected == null || !controllable) return Array.Empty<Unit>();
        IReadOnlyList<Unit> passengers = selected.Cargo;
        return passengers.Count > 0 ? passengers : Array.Empty<Unit>();
    }

    public SelectionGroupFrame(IUiTextureResolver textures) : base("SelectionGroup")
    {
        _iconResolver = textures.Resolve;
        ITexture fill = textures.Resolve(@"UI\Feedback\HpBarConsole\human-healthbar-fill.blp");
        for (int i = Slots - 1; i >= 0; i--)
        {
            var icon = new TextureFrame($"SelIcon{i}");
            var border = new TextureFrame($"SelBorder{i}")
            {
                Texture = textures.Resolve(@"ReplaceableTextures\CommandButtons\BTNTemp.blp"),
                Color = new Vector4(1f, 0.85f, 0.2f, 1f),
                Visible = false,
            };
            var hp = new StatusBarFrame($"SelHp{i}")
            {
                FillColor = new Vector4(0.1f, 0.78f, 0.12f, 1f),
                BackgroundColor = Vector4.Zero,
                BarInset = 0.5f,
                FillTexture = fill,
            };
            var mp = new StatusBarFrame($"SelMp{i}")
            {
                FillColor = new Vector4(0.05f, 0.2f, 0.9f, 1f),
                BackgroundColor = Vector4.Zero,
                BarInset = 0.5f,
                FillTexture = fill,
            };
            _slots[i] = new Slot(icon, border, hp, mp);
            AddChild(border);
            AddChild(icon);
            AddChild(hp);
            AddChild(mp);
        }
        Visible = false;
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        float offsetX = (viewportWidth - War3UiViewport.BaseWidth) * 0.5f;
        float panelTop = viewportHeight - PanelTopFromBottom;
        float originX = offsetX + BoxLeft;
        float rowHeight = CellSize + HpGap + HpH + MpGap + MpH;
        for (int i = 0; i < Slots; i++)
        {
            int column = i % Cols, row = i / Cols;
            float x = originX + column * (CellSize + GapX);
            float y = panelTop + 8f + row * (rowHeight + GapY);
            float scale = 1f;
            if (i == _selectedIndex) scale = SelectedScale;
            else if (i < _selection.Count && PlayerSelectionController.SameCommandSubgroup(
                _selected, _selection[i])) scale = SelectedScale;
            float size = CellSize * scale;
            float centerX = x + CellSize * .5f, centerY = y + CellSize * .5f;
            float iconX = centerX - size * .5f, iconY = centerY - size * .5f;
            ref Slot slot = ref _slots[i];
            slot.Icon.RenderBounds = new UiRect(iconX, iconY, size, size);
            slot.Border.RenderBounds = new UiRect(iconX - 1.5f, iconY - 1.5f,
                size + 3f, size + 3f);
            float barWidth = size;
            float barX = centerX - barWidth * .5f;
            float barHeight = HpH * scale;
            slot.Hp.RenderBounds = new UiRect(barX, iconY + size + HpGap * scale,
                barWidth, barHeight);
            slot.Mp.RenderBounds = new UiRect(barX,
                iconY + size + HpGap * scale + barHeight + MpGap * scale,
                barWidth, MpH * scale);
        }
    }

    public void Update(Unit selected, IReadOnlyList<Unit> selection, bool cargoMode = false)
    {
        _selected = selected;
        _selection.Clear();
        if (selection != null)
            for (int i = 0; i < selection.Count && i < Slots; i++)
                _selection.Add(selection[i]);
        // 面板顺序与框选/Tab 同源：原生按 prio 排序，主位只是被高亮的那个，不占首格。
        if (!cargoMode) NativeSelectionOrder.Sort(_selection);
        bool show = cargoMode || _selection.Count > 1;
        _selectedIndex = -1;
        for (int i = 0; i < _selection.Count; i++)
            if (ReferenceEquals(_selection[i], selected)) { _selectedIndex = i; break; }

        for (int i = 0; i < Slots; i++)
        {
            ref Slot slot = ref _slots[i];
            bool active = show && i < _selection.Count;
            slot.Icon.Visible = active;
            slot.Hp.Visible = slot.Mp.Visible = active;
            slot.Border.Visible = false;
            if (!active) continue;
            Unit unit = _selection[i];
            string icon = unit.Type?.IconPath;
            if (string.IsNullOrWhiteSpace(icon))
                icon = @"ReplaceableTextures\CommandButtons\BTNTemp.blp";
            if (!unit.Alive) icon = NativeCommandButtonBinding.DisabledArt(icon,
                @"ReplaceableTextures\CommandButtonsDisabled\");
            slot.Icon.Texture = _iconResolver(icon);
            slot.Icon.Color = unit.Alive ? Vector4.One : new Vector4(.72f, .72f, .72f, 1f);
            bool selectedSlot = i == _selectedIndex
                || (!cargoMode && PlayerSelectionController.SameCommandSubgroup(selected, unit));
            slot.Border.Visible = selectedSlot;
            bool hasHealth = unit.Alive && unit.MaxHp > 0;
            slot.Hp.Visible = hasHealth;
            slot.Hp.Ratio = hasHealth ? Math.Clamp(unit.Hp / unit.MaxHp, 0f, 1f) : 0f;
            bool hasMana = unit.Alive && unit.MaxMana > 0;
            slot.Mp.Visible = hasMana;
            slot.Mp.Ratio = hasMana ? Math.Clamp(unit.Mana / unit.MaxMana, 0f, 1f) : 0f;
        }
        Visible = show;
    }

    bool IClickable.Enabled => Enabled;

    public override UiFrame HitTest(Vector2 point)
    {
        if (!Visible || !Enabled || MouseTransparent) return null;
        for (int i = 0; i < _selection.Count; i++)
            if (_slots[i].Icon.RenderBounds.Contains(point.X, point.Y)) return this;
        return null;
    }

    void IClickable.OnPointerDown(Vector2 pos)
    {
        for (int i = 0; i < _selection.Count; i++)
            if (_slots[i].Icon.RenderBounds.Contains(pos.X, pos.Y))
            {
                OnUnitClick?.Invoke(_selection[i]);
                return;
            }
    }

    void IClickable.OnPointerUp(Vector2 point) { }
    void IClickable.OnClick(int button) { }
}