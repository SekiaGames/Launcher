namespace Launcher;

/// <summary>
/// 单位变形（妖术变羊、变身恶魔、德鲁伊形态）。对标客户端 Units/Unit.Morph.cs：
/// 变形期间 Type 换成目标类型、生命按比例保留；原来那一份类型与上限留在 MorphOrigin 上，到期还原。
/// </summary>
public sealed partial class Unit
{
    public bool Morphed => MorphPermanent || MorphRemaining > 0f;

    /// <summary>剩余变形时间（秒）；永久形态不用这个。</summary>
    public float MorphRemaining;

    /// <summary>永久形态（德鲁伊熊/乌鸦）：不会自己到期，再施放一次才变回来。</summary>
    public bool MorphPermanent;

    /// <summary>变形前的单位类型（非空即处于变形中）。</summary>
    public UnitType MorphOrigin;

    /// <summary>变形前的生命上限：还原时直接写回，避免英雄属性重算带来的漂移。</summary>
    public float MorphOriginMaxHp;

    /// <summary>变形前保留的生命比例。</summary>
    public float MorphHpRatio = 1f;

    /// <summary>变形前是不是英雄（变羊的兽王要能变回来）。</summary>
    public bool MorphOriginHero;
}
