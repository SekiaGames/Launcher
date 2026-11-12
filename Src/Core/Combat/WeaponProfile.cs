namespace Launcher;

/// <summary>
/// 一套武器的完整参数。对标客户端 Src/Core/Weapon.cs + Weapon.TypeFields.cs。
/// UnitWeapons 里每个单位最多两套（*1 / *2），打不同目标类别时切换。
/// </summary>
public sealed class WeaponProfile
{
    public bool Enabled;
    public AttackType AttackType = AttackType.Normal;
    public TargetKind Targets;
    public string Sound = "";

    public float Range = 100f;
    public float Cooldown = 1.5f;
    public float MinDamage = 5f;
    public float MaxDamage = 10f;

    /// <summary>起手到伤害点的秒数（dmgpt）。</summary>
    public float DamagePoint = 0.5f;
    /// <summary>伤害点之后的后摇（backSw）。</summary>
    public float Backswing = 0.5f;

    public float SplashFull;
    public float SplashHalf;
    public float SplashQuarter;
    public int SplashTargets = 1;
    /// <summary>溅射落点判定的掩码（splashTargs），None 表示退回 Targets。</summary>
    public TargetKind SpillTargets;
    /// <summary>溅射允许的归属关系，默认全部（友军也会被打）。</summary>
    public TargetRelation SpillRelations = TargetRelation.All;
    /// <summary>中/外圈的伤害系数（Hfact / Qfact）。</summary>
    public float SplashHalfRatio = 0.5f;
    public float SplashQuarterRatio = 0.25f;

    public string MissileScene;
    public float MissileSpeed = 900f;
    public float MissileArc;

    public float AcquireRange = 500f;

    public bool CanTarget(TargetKind kind) => Targets == TargetKind.None || (Targets & kind) != 0;
}
