namespace OpenWC3;

public sealed class DevotionAuraBuff(float armorBonus) : AuraBuff
{
    float _applied;
    float _remaining=4;
    public float ArmorBonus=>armorBonus;
    public override string Name => "虔诚";

    // 6f4bc290: buff still attaches to illusions, but the armor contribution
    // is zero when IllusionsGetDefenseBonus is disabled.
    float ComputeContribution()
    {
        var config=Owner?.World?.GameplayConfig;
        if(Owner!=null&&Owner.IsIllusion&&config!=null
            &&!config.IllusionsGetDefenseBonus)return 0;
        return armorBonus;
    }

    // 非叠加护甲(同源光环取最大, 对标 NonStackingStatBuff DEF)
    protected override void OnBuffAdd()
    {
        _applied=ComputeContribution();
        Owner.Stats.AddNonStack("Devotion", _applied, 0, this);
    }
    protected override void OnBuffRemove()
    {
        Owner.Stats.RemoveNonStack("Devotion", this);
        _applied=0;
    }

    public void RefreshParameters(float armor)
    {
        Owner.Stats.RemoveNonStack("Devotion", this);
        armorBonus=armor;
        _applied=ComputeContribution();
        Owner.Stats.AddNonStack("Devotion", _applied, 0, this);
    }

    // 514410 merge: weaker input rejected without refreshing lifetime;
    // equal refreshes the 4s window; stronger refreshes and rebuilds.
    public bool RefreshFrom(DevotionAura aura,int level)
    {
        const float epsilon=.001f; // DAT_6FB59398, native 514410.
        float incoming=aura.ArmorBonus;
        if(incoming<armorBonus&&MathF.Abs(incoming-armorBonus)>=epsilon)return false;
        _remaining=4;
        Source=aura.Owner;Caster=aura.Owner;SourceAura=aura;SourceAbility=aura;AuraLevel=level;
        if(incoming>armorBonus&&MathF.Abs(incoming-armorBonus)>=epsilon)
            RefreshParameters(incoming);
        return true;
    }

    public override void OnTick(float dt)
    {
        _remaining-=dt;
        if(_remaining<=0)Owner.RemoveAbility(this);
    }
}
