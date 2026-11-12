namespace OpenWC3.Game;

/// <summary>CAbilityFeedback（Afbk）：命中时抽取目标魔法并按抽取量附加伤害。</summary>
public sealed class RuntimeFeedbackAbility : Ability,IAttackLaunchModifier
{
    public int AttackEffectPriority=>0x18; // 586fb0 / 586bd0.
    TargetFlags _allowedTargets;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public float MaximumManaDrained { get; private set; }
    public float DamageRatio { get; private set; }
    public float MaximumHeroManaDrained { get; private set; }
    public float HeroDamageRatio { get; private set; }
    public float SummonedDamage { get; private set; }
    public float LastManaDrained { get; private set; }

    public void RefreshFeedbackData()
    {
        int level=Math.Max(1,Level);
        MaximumManaDrained=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0));
        DamageRatio=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0));
        MaximumHeroManaDrained=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataC",level,MaximumManaDrained));
        HeroDamageRatio=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataD",level,DamageRatio));
        SummonedDamage=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataE",level,0));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air|TargetFlags.Enemy;
    }

    public override void OnLevelUp()=>RefreshFeedbackData();
    public override void OnLevelDown()=>RefreshFeedbackData();
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;

    bool Eligible(Unit source,Unit target)=>Level>0&&!IsDisabled&&!IsRuntimeLocked
        &&source!=null&&!source.IsIllusion&&target?.Alive==true&&!target.Removed&&!target.MagicImmune
        &&(source.World?.AreEnemies(source,target)??target.Team!=source.Team)
        &&target.CanBeTargetedBy(source,_allowedTargets,useHeroSpellRules:true);
    public IAttackImpactPayload PrepareAttack(Unit target)
    {
        if(!Eligible(Owner,target))return null;
        bool special=(target.Is(Classification.Summoned)||target.IsIllusion)&&SummonedDamage>0;
        if(target.Mana<=0&&!special)return null; // 586bd0 rejects no-mana ordinary targets.
        return new Shot(this,Owner,MaximumManaDrained,DamageRatio,MaximumHeroManaDrained,
            HeroDamageRatio,SummonedDamage);
    }
    sealed record Shot(RuntimeFeedbackAbility Ability,Unit Source,float Maximum,float Ratio,
        float HeroMaximum,float HeroRatio,float SummonedBonus):IAttackImpactPayload
    {
        public void Apply(Unit target,ref DamageData damage)=>
            Ability.ApplyFeedback(Source,target,ref damage,Maximum,Ratio,HeroMaximum,HeroRatio,SummonedBonus);
    }

    public override void ModifyOutgoingDamage(Unit target,ref DamageData damage)
    {
        if(!Eligible(Owner,target)){LastManaDrained=0;return;}
        ApplyFeedback(Owner,target,ref damage,MaximumManaDrained,DamageRatio,
            MaximumHeroManaDrained,HeroDamageRatio,SummonedDamage);
    }
    void ApplyFeedback(Unit source,Unit target,ref DamageData damage,float maximumMana,float damageRatio,
        float maximumHeroMana,float heroDamageRatio,float summonedDamage)
    {
        LastManaDrained=0;
        if(!damage.IsAttack||source==null||source.IsIllusion||target?.Alive!=true
            ||target.Removed||target.MagicImmune
            ||!(source.World?.AreEnemies(source,target)??target.Team!=source.Team)
            ||!target.CanBeTargetedBy(source,_allowedTargets,useHeroSpellRules:true))return;
        bool specialDamage=(target.Identity.Is(Classification.Summoned)||target.IsIllusion)
            &&summonedDamage>0;
        if(specialDamage)
        {
            damage.BonusAmount+=summonedDamage;
        }
        float maximum=target.IsHero?maximumHeroMana:maximumMana;
        float ratio=target.IsHero?heroDamageRatio:damageRatio;
        LastManaDrained=Math.Min(Math.Max(0,target.Mana),maximum);
        if(LastManaDrained<=0)
        {
            if(specialDamage)SpawnAttackArt(target);
            return;
        }
        target.Mana-=LastManaDrained;
        damage.BonusAmount+=LastManaDrained*ratio;
        SpawnAttackArt(target);
    }

    void SpawnAttackArt(Unit target)
    {
        if(Owner?.World==null)return;
        foreach(string art in SpecialArts)
            SpawnAttachedArt(art,target,SpecialAttachments,1.2f);
    }
}
