using Godot;

namespace OpenWC3;

/// <summary>
/// 致命一击 AOcr（被动）。对标客户端 Src/Skills/CriticalStrike.cs：
/// DataA% 概率造成 DataB 倍伤害（物编 DataA=15 恒定，DataB=2/3/4）。
/// </summary>
public sealed class CriticalStrike : Ability
{
    // 原版 PRD 系数表（4c50b0）：按百分比取档，乘以已尝试次数得到阈值。
    static readonly float[] ProcCoefficients = new uint[]
    {
        0x00000000u,0x3b79096cu,0x3c70d845u,0x3d03126fu,0x3d600d1cu,0x3da7bb30u,0x3de69ad4u,
        0x3e159b3du,0x3e39db23u,0x3e5fa440u,0x3e832ca5u,0x3e973190u,0x3eaaa64cu,0x3ec19653u,
        0x3ed73190u,0x3eebe0dfu,0x3f000000u,0x3f147454u,0x3f2aa64cu,0x3f45460bu,0x3f800000u,
    }.Select(BitConverter.UInt32BitsToSingle).ToArray();

    public static float NativeProcThreshold(float percent, int attempts)
        => ProcCoefficients[Math.Clamp((int)(percent * .2f + .5f), 0, 20)] * attempts;

    public override bool IsPassive => true;

    public override bool MarksCriticalStrike => true;

    public float Chance => Data('A');
    public float Multiplier => Mathf.Max(1f, Data('B'));

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (world == null || !world.RollPercent(Chance)) return raw;
        return raw * Multiplier;
    }
}
