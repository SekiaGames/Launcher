using Godot;
using System;
using System.Globalization;

namespace OpenWC3;

/// <summary>
/// 世界飘字（伤害 / 暴击 / 未命中 / 资源 / 赏金）。数据只在模拟层累积，
/// 渲染由 UI 层的屏幕空间文字统一投影，脱离单位后不再跟随。
/// </summary>
public sealed class FloatingText
{
    public Vector3 Pos;
    public string Text;
    public Color Color;
    public float Life = 0.9f, Age;
    public float OffsetX;
    public Unit VisibilityUnit;
    public War3FloatingTextStyle Style;
    public float WorldHeight = 80f;

    /// <summary>「伤害数字显示」开关产生的普通伤害数字，渲染侧据此做整类开关与迷雾门禁。</summary>
    public bool OrdinaryDamage;

    public float Alpha => Style == null
        ? Mathf.Clamp(1f - Age / Life, 0f, 1f)
        : Style.Alpha * (Age <= Style.FadeStart || Life <= Style.FadeStart
            ? 1f
            : Mathf.Clamp((Life - Age) / (Life - Style.FadeStart), 0f, 1f));

    /// <summary>出字时就判定观察者看得到，之后位置固定；判定含迷雾与隐身/反隐。</summary>
    public static bool CanShow(Unit source)
    {
        if (source?.World == null || source.Hidden || source.Removed || source.IsCargoed) return false;
        World world = source.World;
        if (source.Invisible)
        {
            int viewer = world.ObserverPlayer;
            if (source.Player != viewer && !world.AreAllied(source.Player, viewer)
                && !world.IsDetectedBy(viewer, source)) return false;
        }
        return world.FogVisible == null || world.FogVisible(source.Position.X, source.Position.Z);
    }

    public static void ShowCriticalStrike(Unit source, float amount)
    {
        if (!CanShow(source) || amount <= 0f) return;
        War3FloatingTextStyle style = StyleOf(source).CriticalStrikeText;
        Add(source, Format(((int)amount).ToString(CultureInfo.InvariantCulture) + "!"), style);
    }

    public static void ShowOrdinaryDamage(Unit victim, float amount)
    {
        if (!CanShow(victim) || (int)amount <= 0) return;
        War3FloatingTextStyle style = StyleOf(victim).OrdinaryDamageText;
        Add(victim, Format(Number(amount)), style, ordinaryDamage: true);
    }

    public static void ShowMiss(Unit source)
    {
        if (!CanShow(source)) return;
        War3FloatingTextStyle style = StyleOf(source).MissText;
        Add(source, "未命中", style);
    }

    public static void ShowManaBurn(Unit target, float drained)
    {
        // 客户端这条没有正向数值门禁：合法但抽到 0 点时照样出 -0。
        if (!CanShow(target)) return;
        Add(target, Format("-" + Number(drained)), StyleOf(target).ManaBurnText);
    }

    public static void ShowShadowStrike(Unit target, float amount, bool initial)
    {
        if (!CanShow(target) || (int)amount <= 0) return;
        Add(target, Format(Number(amount) + (initial ? "!" : "")), StyleOf(target).ShadowStrikeText);
    }

    public static void ShowResource(Unit unit, int amount, bool lumber, float heightOffset = 0f)
    {
        if (!CanShow(unit)) return;
        War3GameplayConfig config = StyleOf(unit);
        Add(unit, "+" + Number(amount), lumber ? config.LumberText : config.GoldText, heightOffset);
    }

    public static void ShowBounty(Unit victim, int amount, bool lumber = false)
    {
        // 赏金不判迷雾：击杀反馈点在受害者位置，死亡本身并不压制这条。
        if (victim?.World == null || victim.Removed || amount == 0) return;
        War3GameplayConfig config = StyleOf(victim);
        Add(victim, Format("+" + Number(amount), 6), lumber ? config.LumberBountyText : config.BountyText);
    }

    static War3GameplayConfig StyleOf(Unit unit) => unit?.World?.GameplayConfig ?? War3GameplayConfig.Default;

    static string Number(float amount) => ((int)amount).ToString(CultureInfo.InvariantCulture);
    static string Number(int amount) => amount.ToString(CultureInfo.InvariantCulture);

    static string Format(string text, int bufferSize = 10) => text.Length < bufferSize ? text : text[..(bufferSize - 1)];

    static void Add(Unit unit, string text, War3FloatingTextStyle style, float heightOffset = 0f,
        bool ordinaryDamage = false)
        => AddAt(unit, unit.Position, text, style, heightOffset, ordinaryDamage);

    static void AddAt(Unit unit, Vector3 position, string text, War3FloatingTextStyle style,
        float heightOffset = 0f, bool ordinaryDamage = false)
        => unit.World.Floats.Add(new FloatingText
        {
            Pos = position,
            VisibilityUnit = unit,
            Text = text,
            Style = style,
            Color = style.Color,
            Life = style.Lifetime,
            WorldHeight = position.Y + heightOffset,
            OrdinaryDamage = ordinaryDamage,
        });
}
