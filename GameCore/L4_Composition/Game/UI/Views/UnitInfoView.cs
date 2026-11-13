using OpenWC3.RHI;

namespace OpenWC3.Game.UI.Views;

public readonly record struct UnitInfoView(
    string Name,
    bool IsHero,
    string ClassLine = null,
    string TypeLine = null,
    float XpRatio = 0f,
    string AttackText = null,
    string AttackLevel = null,
    ITexture AttackIcon = null,
    string ArmorText = null,
    string ArmorLevel = null,
    ITexture ArmorIcon = null,
    ITexture PrimaryAttributeIcon = null,
    int? Strength = null,
    int? Agility = null,
    int? Intellect = null);
