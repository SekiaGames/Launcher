namespace OpenWC3;

// ========== 耐久光环 (AOae, Tauren Chieftain - Endurance Aura) ==========
// 原版 AbilityData.slk: DataA=移动速度加成 0.1/0.2/0.3, DataB=攻击速度加成 0.05/0.1/0.15,
// Area=900, targs=air,ground,friend,self。
// 注意: 旧 SkillRegistry 把 AOae 错误映射成了 ThornsAura(荆棘光环, 实为 AEah)。
public sealed class EnduranceAura : AuraAbility
{
    public override string Name => "耐久光环";
    public EnduranceAura()
    {
        OrderId = "enduranceaura";
        Learnable = true;
        MaxLevel = 3;
        Level = 0;
        AffectsAllies = true;
        Radius = 900f;
    }

    public override void OnTick(float dt)
    {
        if(IsDisabled)return;
        // 物编 AOae / AOr2 实测 Area1~4 均为 900, 读得到就不走 fallback ->
        // 改中性值 0 行为不变。Radius=0 时光环扫不到任何目标(距离恒 > 0),
        // "缺 Area"表现为光环静默失效 + 一条 [物编缺字段] 日志, 可直接定位 rawcode。
        Radius = GetLevelFloat("Area", 0, 0f);
        base.OnTick(dt);
    }

    protected override bool CanAffect(Unit target)
    {
        if(target==null||target.Removed||!CanAffectExotic(target,Owner.World))return false;
        return target.CanBeTargetedBy(Owner,ConfiguredTargets|TargetFlags.Alive,useHeroSpellRules:true);
    }

    string BuffRawcode=>GetLevelString("BuffID",fallback:"BOae");
    // Same first-match order and per-candidate BuffID read as Before; index access
    // avoids boxing List<Ability>.Enumerator for every aura target.
    protected override AuraBuff FindExistingBuff(Unit target)
    {
        IReadOnlyList<Ability> abilities=target.Abilities.Items;
        for(int i=0;i<abilities.Count;i++)
            if(abilities[i] is EnduranceAuraBuff buff&&buff.Rawcode==BuffRawcode)
                return buff;
        return null;
    }
    protected override bool RefreshExistingBuff(AuraBuff buff,int level)
    {
        // A weaker source does not replace or refresh a stronger native buff.
        ((EnduranceAuraBuff)buff).RefreshFrom(this,level,
            GetLevelFloat("Data",1),GetLevelFloat("Data",2));
        return true;
    }

    // 物编 AOae: DataA=0.1/0.2/0.3/0.3, DataB=0.05/0.1/0.15/0.15;
    // AOr2(凯恩血蹄 4 级版): DataA=0.1/0.2/0.3/0.4, DataB=0.05/0.1/0.15/0.25。
    // 全部非零 -> fallback 中性化行为不变; 缺字段则表现为"无加成"+ 日志。
    protected override AuraBuff CreateBuff(Unit source, Unit target, int level)
        => new EnduranceAuraBuff
        {
            Source = source,
            Duration = 4f,
            Rawcode = BuffRawcode,
            MoveSpeedPct = GetLevelFloat("Data", 1, 0f),
            AttackSpeedPct = GetLevelFloat("Data", 2, 0f)
        };
}
