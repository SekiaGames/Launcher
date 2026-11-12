namespace OpenWC3.Game;

/// <summary>Aliq replaces eligible attacks with a launch-time Liquid Fire payload.</summary>
public sealed class RuntimeLiquidFireAbility(War3ObjectData objects) : Ability,IAttackLaunchModifier
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
    public bool Eligible(Unit target)=>Owner?.World!=null&&target?.Alive==true
        &&!target.Removed&&target.IsBuilding&&!Owner.World.AreAllied(Owner,target)
        &&target.CanBeTargetedBy(Owner,ConfiguredTargets|TargetFlags.Ground|TargetFlags.Structure,useHeroSpellRules:true);

    public IAttackImpactPayload PrepareAttack(Unit target)
    {
        if(Level<=0||IsDisabled||!Eligible(target))return null;
        // 573180 retains the launch values on the missile. Illusions still
        // create the visual buff, but pass zero damage/slow and allow repair.
        bool illusion=Owner.IsIllusion;
        return new Payload(this,Owner,objects,illusion?0:GetLevelFloat("DataA"),
            illusion?0:GetLevelFloat("DataB"),illusion?0:GetLevelFloat("DataC"),
            illusion||GetLevelFloat("DataD")!=0,GetLevelFloat("Dur"),GetLevelFloat("HeroDur"),
            BuffRawcodes.FirstOrDefault()??"Bliq",Math.Max(1,Level),MissileArt);
    }

    sealed record Payload(RuntimeLiquidFireAbility Ability,Unit Source,War3ObjectData Objects,
        float Damage,float MoveSlow,float AttackSlow,bool RepairAllowed,float Duration,float HeroDuration,
        string BuffId,int Level,string MissileArtOverride) : IAttackImpactPayload
    {
        public void Apply(Unit target,ref DamageData damage)
        {
            if(!Ability.Eligible(target))return;
            var existing=target.Abilities.OfType<RuntimeLiquidFireBuff>()
                .FirstOrDefault(buff=>buff.Rawcode==BuffId);
            float duration=target.IsHero?HeroDuration:Duration;
            if(existing!=null)
            {
                existing.Reapply(Damage,MoveSlow,AttackSlow,RepairAllowed,duration,Source,Ability);
                return;
            }
            var buff=new RuntimeLiquidFireBuff(Damage,MoveSlow,AttackSlow,RepairAllowed);
            buff.BindBuffObjectData(BuffId,Objects.GetBuff(BuffId),Level,Source,Ability);
            buff.Duration=duration;buff.Permanent=duration<=0;
            target.AddAbility(buff);
        }
    }
}

public sealed class RuntimeLiquidFireBuff(float damage,float moveSlow,float attackSlow,bool repairAllowed) : Buff
{
    float _nextDamage=.01f;
    float _moveApplied,_attackApplied;
    bool _blocksRepair;
    public float DamagePerSecond { get; private set; }=damage;
    public bool RepairAllowed { get; private set; }=repairAllowed;
    public override string Name=>ObjectData?.GetString("Bufftip","Liquid Fire")??"Liquid Fire";
    public override bool Positive=>false;
    public override bool Magic=>false;
    public override bool Physical=>true;
    public override bool AutoDispel=>false;
    protected override void OnBuffAdd()=>ApplyModifiers(moveSlow,attackSlow);
    void ApplyModifiers(float movement,float attack)
    {
        Owner.SpeedBonus+=_moveApplied;Owner.AttackSpeedBonusPct+=_attackApplied;
        _moveApplied=Owner.MoveSpeedBase*movement;_attackApplied=attack;
        Owner.SpeedBonus-=_moveApplied;Owner.AttackSpeedBonusPct-=_attackApplied;
        if(!RepairAllowed&&!_blocksRepair)
        {Owner.State.Add(StateModType.RepairDisabled);_blocksRepair=true;}
    }
    protected override void OnBuffRemove()
    {
        Owner.SpeedBonus+=_moveApplied;Owner.AttackSpeedBonusPct+=_attackApplied;
        _moveApplied=0;_attackApplied=0;
        if(_blocksRepair){Owner.State.Remove(StateModType.RepairDisabled);_blocksRepair=false;}
    }
    public void Reapply(float damage,float movement,float attack,bool allowRepair,float duration,Unit source,Ability ability)
    {
        // 580160 sets the allow-repair bit at creation; 58bae0 only clears it
        // on refresh. A later allow-repair hit cannot release an existing block.
        RepairAllowed&=allowRepair;
        ApplyModifiers(movement,attack);
        if(duration>0&&(Permanent||DamagePerSecond*RemainingSeconds>damage*duration))return;
        DamagePerSecond=damage;Caster=source;SourceAbility=ability;
        Duration=duration<=0?0:Math.Max(duration,RemainingSeconds);Permanent=duration<=0;
        RefreshExpiration();
        // Reapplication extends the life timer; it does not reset the DoT timer.
    }
    public override void OnTick(float dt)
    {
        base.OnTick(dt);
        if(!Owner.Abilities.Contains(this)||!Owner.Alive)return;
        _nextDamage-=dt;
        while(_nextDamage<=0&&Owner.Alive&&Owner.Abilities.Contains(this))
        {
            _nextDamage+=1;
            if(DamagePerSecond>0)Owner.TakeDamage(new DamageData
            {Source=Caster,Amount=DamagePerSecond,AttackType=AttackType.Spells,DamageType=DamageType.Fire});
            else if(DamagePerSecond<0)Owner.Heal(-DamagePerSecond);
        }
    }
}
