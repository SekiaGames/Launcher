using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 烈焰风暴 AHfs（血法师）。对标 Wiki RuntimeFlameStrikeAbility：
/// 点地面 → 前 FullDamageDuration（HeroDur 2.67s）每 DataB(0.33s) 结算一次 DataA 伤害，
/// 之后每 DataD(1s) 结算一次 DataC(4) 的余烬伤害，总共 Dur(9s)，建筑乘 DataE(0.75)。
/// 不引导，施法者可以立刻走开（地面效果挂在 <see cref="IGroundSpell"/> 上推进）。
/// </summary>
public sealed class RuntimeFlameStrikeAbility : Ability
{
    readonly War3ObjectData _objects;
    IReadOnlyList<string> _eruptionArts = Array.Empty<string>();
    string _emberArt = "";
    IReadOnlyList<string> _preloadArts = Array.Empty<string>();

    public RuntimeFlameStrikeAbility(War3ObjectData objects) => _objects = objects;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override IReadOnlyList<string> RuntimePreloadArts
    {
        get
        {
            RefreshArtData();
            return _preloadArts;
        }
    }

    public float FullDamage => Data('A');
    public float FullInterval => Data('B') > 0f ? Data('B') : 0.33f;
    public float HalfDamage => Data('C');
    public float HalfInterval => Data('D') > 0f ? Data('D') : 1f;
    public float BuildingFactor => Data('E') > 0f ? Data('E') : 0.75f;
    public float TotalDuration => Duration > 0f ? Duration : 9f;
    public float FullDuration => HeroDuration > 0f ? HeroDuration : TotalDuration;
    public float Radius => Area > 0f ? Area : 200f;

    public override void OnLevelUp() => RefreshArtData();
    public override void OnLevelDown() => RefreshArtData();

    void RefreshArtData()
    {
        int level = Mathf.Max(1, Level);
        War3TypeData abilityData = War3ObjectData.GetAbility(Type?.Id ?? Rawcode)
            ?? War3ObjectData.GetAbility(Type?.Code ?? "");
        _eruptionArts = AbilityArt.Arts(abilityData, 1, "Specialart", "asat");
        string effectId = RuntimeAbilityFactory.ReadEffectRawcode(ObjectData, level);
        _emberArt = AbilityArt.FirstArt(_objects?.GetBuff(effectId), 1, "Effectart", "");

        var preload = new List<string>(_eruptionArts);
        if (!string.IsNullOrWhiteSpace(_emberArt)) preload.Add(_emberArt);
        _preloadArts = preload;
    }

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        float range = CastRange > 0f ? CastRange : 800f;
        return FlatDistance(Owner.Position, point) <= range;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        RefreshArtData();
        Vector3 flattened = Flatten(point);
        if (_eruptionArts.Count > 0)
        {
            string art = _eruptionArts[world.Rng.Next(_eruptionArts.Count)];
            world.AddSpecialEffect(art, new Vec2(flattened.X, flattened.Z), lifeSpan: TotalDuration);
        }

        world.AddGroundSpell(new FlameStrikeBurn
        {
            Source = Owner,
            Point = flattened,
            Radius = Radius,
            TotalDuration = TotalDuration,
            FullDuration = Mathf.Min(FullDuration, TotalDuration),
            FullDamage = FullDamage,
            FullInterval = FullInterval,
            HalfDamage = HalfDamage,
            HalfInterval = HalfInterval,
            BuildingFactor = BuildingFactor,
            Timer = FullInterval,
            EmberArt = _emberArt,
        });
        return true;
    }

    /// <summary>AI：朝射程内最近的敌人脚下砸；半径里的敌人越多越值。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, AiTactics.LeadPoint(target));
    }
}
