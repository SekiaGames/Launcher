using System;
using System.Collections.Generic;
using System.Numerics;
using OpenWC3.Game.UI.Views;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed class UnitInfoPanelFrame : UiContainer
{
    private const float PanelWidth = 230.4f, PanelHeight = 153.6f;
    private const float ContentTopInset = 10f;
    private const float HeroColumnTop = 47.36f, HeroColumnHeight = 80f;
    private const float IconSize = 38f, IconValueGap = 46f, IconValueWidth = 84f;
    private const float LevelSize = 12f;
    private const float AttrIcon = 38f, AttrX = 124f, AttrTextX = 164f, AttrTextWidth = 62f;
    private const float ContentY = 44f;
    private const float AttrTextDx = 4.2f + 38f * 0.04f;
    private const float AttrFrameStep = 21.6f;
    private const float NameHeight = 20f, NameFont = 16.64f;
    private const float XpBarTop = 22f, XpBarHeight = 20f;
    private const float ClassTop = 27f, ClassFont = 10.88f;
    internal const int BuffSlotCount = 8;
    internal const float BuffIconSize = 19.2f;
    private const float StatusIconTop = PanelHeight - ContentTopInset - 6f - BuffIconSize;

    private readonly StringFrame _name;
    private readonly StatusBarFrame _xpBar;
    private readonly StringFrame _classLine;
    private readonly StringFrame _typeLine;
    private readonly TextureFrame _attackIcon;
    private readonly StringFrame _attackLabel;
    private readonly StringFrame _attackLevel;
    private readonly StringFrame _attackValue;
    private readonly TextureFrame _armorIcon;
    private readonly StringFrame _armorLabel;
    private readonly StringFrame _armorLevel;
    private readonly StringFrame _armorValue;
    private readonly TextureFrame _primaryAttrIcon;
    private readonly StringFrame _strLabel, _agiLabel, _intLabel;
    private readonly StringFrame _strValue, _agiValue, _intValue;
    private readonly StringFrame _statusLabel;
    private readonly TextureFrame[] _buffIcons = new TextureFrame[BuffSlotCount];
    private readonly string[] _buffTitles = new string[BuffSlotCount];
    private readonly string[] _buffBodies = new string[BuffSlotCount];
    private readonly Func<string, ITexture> _iconResolver;

    private UiFrame[] _targets;

    public UnitInfoPanelFrame(IUiTextureResolver textures, War3UiSkin skin) : base("UnitInfoPanel")
    {
        _iconResolver = textures.Resolve;
        var shadow = new Vector4(0, 0, 0, 0.9f);
        var label = new Vector4(0.25f, 0.82f, 0.25f, 1f);

        _name = new StringFrame("UnitName")
        {
            FontSize = NameFont, Alignment = TextAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Middle, FontShadowColor = shadow,
            FontShadowOffset = new Vector2(2.56f, 2.56f),
        };
        _xpBar = new StatusBarFrame("UnitXpBar")
        {
            FillColor = new Vector4(138f / 255f, 0f, 131f / 255f, 1f),
            FillTexture = textures.Resolve(skin.Get("SimpleXpBarConsole", "")),
            BorderTexture = textures.Resolve(skin.Get("SimpleXpBarBorder", "")),
        };
        _classLine = new StringFrame("UnitClass")
        {
            FontSize = ClassFont, Alignment = TextAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Middle,
            Color = Vector4.One, FontShadowColor = shadow,
        };
        _typeLine = new StringFrame("UnitTypeLine")
        {
            FontSize = ClassFont, Alignment = TextAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Middle,
            Color = new Vector4(0.7f, 0.7f, 0.7f, 1f), FontShadowColor = shadow,
        };
        _attackIcon = new TextureFrame("UnitAttackIcon") { MouseTransparent = false };
        _attackLabel = new StringFrame("UnitAttackLabel") { Text = "攻击力：", Color = label, FontSize = 10.35f, FontShadowColor = shadow };
        _attackLevel = new StringFrame("UnitAttackLevel") { FontSize = LevelSize, Alignment = TextAlignment.Right, Color = new Vector4(1f, 0.85f, 0.3f, 1f), FontShadowColor = shadow };
        _attackValue = new StringFrame("UnitAttackValue") { FontSize = 12.65f, FontShadowColor = shadow };
        _armorIcon = new TextureFrame("UnitArmorIcon") { MouseTransparent = false };
        _armorLabel = new StringFrame("UnitArmorLabel") { Text = "护甲：", Color = label, FontSize = 10.35f, FontShadowColor = shadow };
        _armorLevel = new StringFrame("UnitArmorLevel") { FontSize = LevelSize, Alignment = TextAlignment.Right, Color = new Vector4(1f, 0.85f, 0.3f, 1f), FontShadowColor = shadow };
        _armorValue = new StringFrame("UnitArmorValue") { FontSize = 12.65f, FontShadowColor = shadow };
        _statusLabel = new StringFrame("UnitStatusLabel") { Text = "状态：", Color = label, FontSize = 10.35f, FontShadowColor = shadow };
        _primaryAttrIcon = new TextureFrame("UnitPrimaryAttributeIcon");
        _strLabel = new StringFrame("UnitStrLabel") { Text = "力量：", Color = label, FontSize = 10.35f, FontShadowColor = shadow };
        _agiLabel = new StringFrame("UnitAgiLabel") { Text = "敏捷度：", Color = label, FontSize = 10.35f, FontShadowColor = shadow };
        _intLabel = new StringFrame("UnitIntLabel") { Text = "智力：", Color = label, FontSize = 10.35f, FontShadowColor = shadow };
        _strValue = new StringFrame("UnitStrValue") { FontSize = 12.65f, FontShadowColor = shadow };
        _agiValue = new StringFrame("UnitAgiValue") { FontSize = 12.65f, FontShadowColor = shadow };
        _intValue = new StringFrame("UnitIntValue") { FontSize = 12.65f, FontShadowColor = shadow };

        AddChild(_name);
        AddChild(_xpBar);
        AddChild(_classLine);
        AddChild(_typeLine);
        AddChild(_attackIcon);
        AddChild(_attackLabel);
        AddChild(_attackLevel);
        AddChild(_attackValue);
        AddChild(_armorIcon);
        AddChild(_armorLabel);
        AddChild(_armorLevel);
        AddChild(_armorValue);
        AddChild(_primaryAttrIcon);
        AddChild(_strLabel);
        AddChild(_strValue);
        AddChild(_agiLabel);
        AddChild(_agiValue);
        AddChild(_intLabel);
        AddChild(_intValue);
        AddChild(_statusLabel);
        for (int i = 0; i < BuffSlotCount; i++)
        {
            _buffIcons[i] = new TextureFrame($"BuffIcon{i}") { Visible = false, MouseTransparent = false };
            AddChild(_buffIcons[i]);
        }

        Visible = false;
    }

    public static UiRect PanelBounds(float viewportWidth, float viewportHeight) =>
        new((viewportWidth - PanelWidth) * 0.5f, viewportHeight - PanelHeight, PanelWidth, PanelHeight);

    public void Layout(float viewportWidth, float viewportHeight)
    {
        float x = (viewportWidth - PanelWidth) * 0.5f;
        float top = viewportHeight - PanelHeight + ContentTopInset;

        float atkY = ContentY + IconSize * 0.1f;
        _name.RenderBounds = new UiRect(x, top, PanelWidth, NameHeight);
        _xpBar.RenderBounds = new UiRect(x, top + XpBarTop, PanelWidth, XpBarHeight);
        _classLine.RenderBounds = new UiRect(x, top + ClassTop, PanelWidth, 14);
        _typeLine.RenderBounds = new UiRect(x, top + NameHeight + IconSize * 0.2f, PanelWidth, 12);

        _attackIcon.RenderBounds = new UiRect(x + 6, top + atkY, IconSize, IconSize);
        _attackLabel.RenderBounds = new UiRect(x + 6 + IconValueGap - IconSize * 0.08f,
            top + atkY + 3 + IconSize * 0.04f, IconValueWidth, 12);
        _attackLevel.RenderBounds = new UiRect(x + 6 + IconSize - LevelSize,
            top + atkY + IconSize - LevelSize, LevelSize, LevelSize);
        _attackValue.RenderBounds = new UiRect(x + 6 + IconValueGap - IconSize * 0.08f + AttrTextDx,
            top + atkY + 14 + IconSize * 0.08f, IconValueWidth, 14);

        float armorY = atkY + IconSize;
        _armorIcon.RenderBounds = new UiRect(x + 6, top + armorY, IconSize, IconSize);
        _armorLabel.RenderBounds = new UiRect(x + 6 + IconValueGap - IconSize * 0.08f,
            top + armorY + 3 + IconSize * 0.04f, IconValueWidth, 12);
        _armorLevel.RenderBounds = new UiRect(x + 6 + IconSize - LevelSize,
            top + armorY + IconSize - LevelSize, LevelSize, LevelSize);
        _armorValue.RenderBounds = new UiRect(x + 6 + IconValueGap - IconSize * 0.08f + AttrTextDx,
            top + armorY + 14 + IconSize * 0.08f, IconValueWidth, 14);

        float attrY = HeroColumnTop + (HeroColumnHeight - AttrIcon) * 0.5f;
        _primaryAttrIcon.RenderBounds = new UiRect(x + AttrX, top + attrY, AttrIcon, AttrIcon);
        StringFrame[] labels = { _strLabel, _agiLabel, _intLabel };
        StringFrame[] values = { _strValue, _agiValue, _intValue };
        for (int i = 0; i < 3; i++)
        {
            float rowTop = attrY + i * (AttrFrameStep * 1.2f) - 4 - AttrIcon * 0.2f + IconSize * 0.04f;
            labels[i].RenderBounds = new UiRect(x + AttrTextX, top + rowTop, AttrTextWidth, 12);
            values[i].RenderBounds = new UiRect(x + AttrTextX + AttrTextDx, top + rowTop + 11,
                AttrTextWidth - 5, 14);
        }

        _statusLabel.RenderBounds = new UiRect(x + 6 + IconValueGap,
            top + StatusIconTop + (BuffIconSize - 12f) / 2f, 24, 12);
        for (int i = 0; i < BuffSlotCount; i++)
        {
            _buffIcons[i].RenderBounds = new UiRect(x + 6 + IconValueGap + 24 + i * BuffIconSize,
                top + StatusIconTop, BuffIconSize, BuffIconSize);
        }
    }

    public void Update(UnitInfoView? view)
    {
        if (view is not { } info)
        {
            Visible = false;
            return;
        }

        Visible = true;
        _name.Text = info.Name ?? "";
        _classLine.Text = info.ClassLine ?? "";
        _classLine.Visible = info.IsHero;
        _typeLine.Text = info.TypeLine ?? "";
        _typeLine.Visible = !info.IsHero && !string.IsNullOrEmpty(info.TypeLine);

        _xpBar.Visible = info.IsHero;
        _xpBar.Ratio = info.XpRatio;

        _attackIcon.Texture = info.AttackIcon;
        _attackValue.Text = info.AttackText ?? "";
        _attackLabel.Visible = true;
        _attackLevel.Text = info.AttackLevel ?? "";
        _attackLevel.Visible = info.IsHero;

        _armorIcon.Texture = info.ArmorIcon;
        _armorValue.Text = info.ArmorText ?? "";
        _armorLabel.Visible = true;
        _armorLevel.Text = info.ArmorLevel ?? "";
        _armorLevel.Visible = info.IsHero;

        _primaryAttrIcon.Texture = info.PrimaryAttributeIcon;
        _strValue.Text = info.Strength?.ToString() ?? "";
        _agiValue.Text = info.Agility?.ToString() ?? "";
        _intValue.Text = info.Intellect?.ToString() ?? "";
        _primaryAttrIcon.Visible = info.IsHero;
        _strLabel.Visible = _strValue.Visible = info.IsHero;
        _agiLabel.Visible = _agiValue.Visible = info.IsHero;
        _intLabel.Visible = _intValue.Visible = info.IsHero;

        _statusLabel.Visible = true;
    }

    public void SetBuffs(IReadOnlyList<BuffEntry> buffs)
    {
        for (int slot = 0; slot < BuffSlotCount; slot++)
        {
            _buffIcons[slot].Visible = false;
            _buffIcons[slot].Texture = null;
            _buffTitles[slot] = null;
            _buffBodies[slot] = null;
        }
        if (buffs == null) return;

        var identities = new HashSet<string>(StringComparer.Ordinal);
        int filled = 0;
        foreach (BuffEntry entry in buffs)
        {
            if (entry == null) continue;
            string art = entry.IconArt?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(art) || art == "_") continue;
            if (!art.Contains('\\') && !art.Contains('/') && !art.Contains(','))
                art = @"ReplaceableTextures\CommandButtons\" + art;

            ITexture texture = _iconResolver?.Invoke(art);
            if (texture == null) continue;
            if (!string.IsNullOrEmpty(entry.Identity) && !identities.Add(entry.Identity)) continue;

            _buffIcons[filled].Texture = texture;
            _buffIcons[filled].Visible = true;
            _buffTitles[filled] = entry.Title ?? entry.Name ?? "";
            _buffBodies[filled] = entry.Description ?? "";
            filled++;
            if (filled == BuffSlotCount) break;
        }
    }

    public bool TryGetBuffTooltip(int slot, out string title, out string body)
    {
        title = body = "";
        if (slot < 0 || slot >= BuffSlotCount || !_buffIcons[slot].Visible) return false;
        title = _buffTitles[slot];
        body = _buffBodies[slot];
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (string.IsNullOrWhiteSpace(body)) body = title;
        return true;
    }

    internal bool BuffIconVisible(int slot) =>
        slot >= 0 && slot < BuffSlotCount && _buffIcons[slot].Visible;

    internal UiRect BuffIconBounds(int slot) =>
        slot >= 0 && slot < BuffSlotCount ? _buffIcons[slot].RenderBounds : default;
}
