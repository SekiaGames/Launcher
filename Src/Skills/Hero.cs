namespace OpenWC3;

/// <summary>
/// 英雄三围视图。客户端 Hero 是挂在技能表里的能力对象（439 行，含经验/升级/复活）；
/// Launcher 的英雄状态在 Unit 上，这里只提供同名的三围读写入口，供客户端技能按
/// `Abilities.Get<Hero>()` 的口径取属性。
/// </summary>
public sealed class Hero : Ability
{
    public override string Name => "英雄";

    public readonly HeroStat Str;
    public readonly HeroStat Agi;
    public readonly HeroStat Int;

    public Hero() : this(null) { }

    public Hero(Unit owner)
    {
        Owner = owner;
        Str = new HeroStat(owner, 0);
        Agi = new HeroStat(owner, 1);
        Int = new HeroStat(owner, 2);
    }

    public int HeroLevel => Owner?.Level ?? 1;

    public int SkillPoints => Owner?.SkillPoints ?? 0;

    public static Hero Of(Unit unit) => unit != null && unit.IsHero ? new Hero(unit) : null;

    /// <summary>把三围加成写回单位。</summary>
    public void Recalculate()
    {
        if (Owner == null) return;
        Owner.Strength = (int)(Str.Base + Str.Bonus);
        Owner.Agility = (int)(Agi.Base + Agi.Bonus);
        Owner.Intelligence = (int)(Int.Base + Int.Bonus);
    }

    public sealed class HeroStat
    {
        readonly Unit _owner;
        readonly int _index;

        public HeroStat(Unit owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        public float Base
        {
            get => _owner == null ? 0f : (_index switch
            {
                0 => _owner.Strength,
                1 => _owner.Agility,
                _ => _owner.Intelligence,
            });
            set
            {
                if (_owner == null) return;
                if (_index == 0) _owner.Strength = (int)value;
                else if (_index == 1) _owner.Agility = (int)value;
                else _owner.Intelligence = (int)value;
            }
        }

        public float Bonus;

        public float Total => Base + Bonus;
    }
}
