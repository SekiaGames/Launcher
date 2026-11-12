using Godot;

namespace Launcher;

/// <summary>
/// 献祭 AEim（恶魔猎手，开关型）。开启时每秒对身边 Area(160) 内地面敌人造成 DataA(10/15/20) 伤害，
/// 同时每秒烧掉 DataB(7) 点法力；法力见底自动关闭。开启瞬间扣物编 Cost(25)。
/// 物编：Cool=0 Area=160 DataA=10/15/20 DataB=7 DataC=10（关闭后余烬时长，未做）。
/// </summary>
public sealed class RuntimeImmolationAbility : Ability
{
    public override bool IsToggle => true;
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    private float _pulse;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        if (ToggledOn) return false;

        _pulse = 0f;
        SetToggled(world, true);
        return true;
    }

    public override void OnToggle(World world, bool on)
    {
        _pulse = 0f;
    }

    /// <summary>每 tick 扣法力，按 1 秒一次结算伤害（原版是每秒一跳）。</summary>
    public override void OnUpkeep(World world)
    {
        if (Owner == null || !Owner.Alive || world == null)
        {
            if (ToggledOn) SetToggled(world, false);
            return;
        }

        float drain = Data('B');
        if (drain > 0f)
        {
            if (Owner.Mana <= 0f)
            {
                SetToggled(world, false);
                return;
            }
            Owner.Mana = Mathf.Max(0f, Owner.Mana - drain * World.Tick);
        }

        _pulse += World.Tick;
        if (_pulse < 1f) return;
        _pulse -= 1f;

        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Area, hitBuildings: false))
        {
            if (victim.Type != null && victim.Type.Flying) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Data('A'), AttackType.Spells, DamageType.Magic));
        }
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null) return false;

        int nearby = 0;
        bool anyHero = false;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive) continue;
            if (other.Gold > 0) continue;
            if (world.AreAllied(Owner.Player, other.Player)) continue;
            if (Owner.DistanceTo(other) > Area + other.Radius) continue;
            nearby++;
            if (other.IsHero) anyHero = true;
        }

        float drain = Data('B');
        if (ToggledOn)
        {
            if (nearby > 0 && Owner.Mana >= drain * 2f) return false;
            return world.TryCast(Owner, this, null, Owner.Position);
        }

        if (nearby < 2 && !anyHero) return false;
        if (Owner.Mana < ManaCost + drain * 3f) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
