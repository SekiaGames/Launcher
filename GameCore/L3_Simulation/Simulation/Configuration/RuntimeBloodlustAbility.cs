namespace OpenWC3.Game;

/// <summary>Bloodlust（Ablo）：单位目标攻击速度、移动速度和模型缩放 Buff。</summary>
public sealed class RuntimeBloodlustAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;
    IReadOnlyList<string> _runtimePreloadArts=Array.Empty<string>();

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority=>AbilityCastPriority.Support;
    public override bool SupportsAutoCast=>true;
    public override IReadOnlyList<string> RuntimePreloadArts=>_runtimePreloadArts;
    public float AttackSpeedFactor { get; private set; }
    public float MoveSpeedFactor { get; private set; }
    public float ScaleFactor { get; private set; }
    public float NormalDuration { get; private set; }
    public float HeroDuration { get; private set; }
    public string BloodlustBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshBloodlustData()
    {
        int level=Math.Max(1,Level);
        AttackSpeedFactor=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0);
        MoveSpeedFactor=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0);
        ScaleFactor=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataC",level,0);
        NormalDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        HeroDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"HeroDur",level,NormalDuration));
        BloodlustBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(BloodlustBuffRawcode.Length>4)
            BloodlustBuffRawcode=BloodlustBuffRawcode[..4];
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Player|TargetFlags.Ally|TargetFlags.Self|TargetFlags.Alive
                |TargetFlags.Vulnerable;
        BuffTypeData data=objects?.GetBuff(BloodlustBuffRawcode);
        string art=AbilityArt.FirstArt(data,BloodlustBuffRawcode,level,"TargetArt","atat");
        _runtimePreloadArts=string.IsNullOrWhiteSpace(art)
            ?Array.Empty<string>():new[]{art};
    }

    public override void OnLevelUp()=>RefreshBloodlustData();
    public override void OnLevelDown()=>RefreshBloodlustData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>
        target?.Alive==true&&target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    public override bool TryAutoCast(World world)
    {
        if(!HasEnemyNearby(world,900f))return false;

        Unit best=null;
        float bestDistance=float.MaxValue;
        foreach(Unit other in world.Units)
        {
            if(other==null||!other.Alive||other.IsBuilding)continue;
            if(!world.AreAllied(Owner.Player,other.Player))continue;
            if(other.Type?.CanAttack!=true)continue;
            if(other.GetAbility<RuntimeBloodlustBuff>()!=null||!InRange(other))continue;

            float distance=Owner.DistanceTo(other);
            if(distance>=bestDistance)continue;
            bestDistance=distance;
            best=other;
        }
        if(best==null)return false;
        return world.TryCast(Owner,this,best,best.Position);
    }

    public override void OnEffect(Unit target,Vec2 point)
        =>ApplyBuff(objects,this,Owner,target,BloodlustBuffRawcode,AttackSpeedFactor,
            MoveSpeedFactor,ScaleFactor,NormalDuration,HeroDuration);

    internal static void ApplyBuff(War3ObjectData objects,Ability source,Unit caster,Unit target,
        string rawcode,float attack,float move,float scale,float normalDuration,float heroDuration)
    {
        if(target?.Alive!=true)return;
        var existing=target.GetAbility<RuntimeBloodlustBuff>();
        string boundRawcode=string.IsNullOrEmpty(rawcode)?existing?.Rawcode??"":rawcode;
        IRuntimeObjectData data=objects?.GetBuff(boundRawcode)??existing?.ObjectData??source.ObjectData;
        if(existing!=null)
        {
            float activeDuration=existing.Duration;
            existing.BindBuffObjectData(boundRawcode,data,Math.Max(1,source.Level),caster,source);
            existing.Duration=activeDuration;
            existing.Refresh(attack,move,target.UsesHeroSpellRules?heroDuration:normalDuration);
            return;
        }
        var buff=new RuntimeBloodlustBuff(attack,move,scale);
        buff.BindBuffObjectData(boundRawcode,data,Math.Max(1,source.Level),caster,source);
        buff.Owner=target;buff.ApplyDuration(normalDuration,heroDuration);
        buff.Permanent=buff.Duration<=0f;
        target.AddAbility(buff);
    }
}

public sealed class RuntimeBloodlustBuff(
    float attackSpeedFactor,float moveSpeedFactor,float scaleFactor) : Buff
{
    float _appliedAttack;
    float _appliedMove;
    float _scaleMultiplier=1;

    public override string Name=>
        ObjectData?.GetString("Bufftip","Bloodlust")??"Bloodlust";
    public override bool IsHarmful=>false;
    float _attackFactor=attackSpeedFactor,_moveFactor=moveSpeedFactor;
    public float AttackSpeedFactor=>_attackFactor;
    public float MoveSpeedFactor=>_moveFactor;
    public float ScaleFactor=>scaleFactor;

    protected override void OnBuffAdd()
    {
        _appliedAttack=_attackFactor;
        _appliedMove=Owner.MoveSpeedBase*_moveFactor;
        _scaleMultiplier=Math.Max(.01f,1f+scaleFactor);
        Owner.AttackSpeedBonusPct+=_appliedAttack;
        Owner.SpeedBonus+=_appliedMove;
        Owner.VisualScale*=_scaleMultiplier;
        if(Owner.View?.Root!=null)Owner.View.Root.Scale*=_scaleMultiplier;
    }

    /// <summary>514840 refreshes duration and speed fields; it does not replace scale.</summary>
    public void Refresh(float attack,float move,float duration)
    {
        Owner.AttackSpeedBonusPct-=_appliedAttack;Owner.SpeedBonus-=_appliedMove;
        _attackFactor=attack;_moveFactor=move;
        _appliedAttack=attack;_appliedMove=Owner.MoveSpeedBase*move;
        Owner.AttackSpeedBonusPct+=_appliedAttack;Owner.SpeedBonus+=_appliedMove;
        if(duration<=0)
        {Permanent=true;Duration=0;Remaining=0;}
        else if(Permanent||duration>RemainingSeconds)
        {Permanent=false;Duration=duration;RefreshExpiration();}
    }

    protected override void OnBuffRemove()
    {
        Owner.AttackSpeedBonusPct-=_appliedAttack;
        Owner.SpeedBonus-=_appliedMove;
        Owner.VisualScale/=_scaleMultiplier;
        if(Owner.View?.Root!=null)Owner.View.Root.Scale/=_scaleMultiplier;
        _appliedAttack=0;
        _appliedMove=0;
        _scaleMultiplier=1;
    }

    public override bool OnTick(World world,float delta)=>Permanent||base.OnTick(world,delta);
}
