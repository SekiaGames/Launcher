namespace OpenWC3.Game;

/// <summary>Sleep（AUsl）：单位睡眠；DataA 秒内无敌，之后受伤会唤醒。</summary>
public sealed class RuntimeSleepAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Unit;
    public float InvulnerabilityDuration { get; private set; }
    public float NormalDuration { get; private set; }
    public float HeroDuration { get; private set; }
    public string SleepBuffRawcode { get; private set; }="";
    public string ProtectionBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshSleepData()
    {
        int level=Math.Max(1,Level);
        InvulnerabilityDuration=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0);
        NormalDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        HeroDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"HeroDur",level,NormalDuration));
        var buffs=RuntimeAbilityFactory.ReadString(ObjectData,"BuffID",level)
            .SplitConfig(',',StringSplitOptions.TrimEntries);
        SleepBuffRawcode=Normalize(buffs.ElementAtOrDefault(0));
        ProtectionBuffRawcode=Normalize(buffs.ElementAtOrDefault(1));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if((_allowedTargets&TargetRules.Shapes)==0)
            _allowedTargets|=TargetFlags.Ground|TargetFlags.Air;
        _allowedTargets|=TargetFlags.Alive|TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshSleepData();
    public override void OnLevelDown()=>RefreshSleepData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>
        Owner?.World!=null&&target?.Alive==true&&!target.Removed
        &&target.World==Owner.World&&NativeSpellTargetRules.Matches(this,target)
        &&target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);

    public override void OnEffect(Unit target,Vec2 point)
    {
        if(Owner?.World==null||target?.Alive!=true||target.Removed
            ||target.World!=Owner.World)return;
        float duration=target.UsesHeroSpellRules?HeroDuration:NormalDuration;
        var existing=target.GetAbility<RuntimeSleepBuff>();
        if(existing!=null)
        {
            BindSleepBuff(existing,SleepBuffRawcode);
            existing.RefreshFrom(this,duration);
        }
        else
        {
            var buff=new RuntimeSleepBuff();
            BindSleepBuff(buff,SleepBuffRawcode);
            buff.Duration=duration;
            buff.Permanent=duration<=0;
            target.AddAbility(buff);
        }
        var protection=target.GetAbility<RuntimeSleepProtectionBuff>();
        if(protection!=null)
        {
            BindSleepBuff(protection,ProtectionBuffRawcode);
            protection.RefreshFrom(this,InvulnerabilityDuration);
        }
        else
        {
            protection=new RuntimeSleepProtectionBuff();
            BindSleepBuff(protection,ProtectionBuffRawcode);
            protection.Duration=InvulnerabilityDuration;
            protection.Permanent=InvulnerabilityDuration<=0;
            target.AddAbility(protection);
        }
    }

    void BindSleepBuff(Buff buff,string rawcode)
    {
        BuffTypeData data=objects?.GetBuff(rawcode);
        if(data!=null)buff.BindBuffObjectData(rawcode,data,
            Math.Max(1,Level),Owner,this);
        else buff.BindObjectData(rawcode,ObjectData,
            Math.Max(1,Level));
        buff.Caster=Owner;
        buff.SourceAbility=this;
    }

    static string Normalize(string rawcode)=>string.IsNullOrWhiteSpace(rawcode)
        ||rawcode=="-"?"":rawcode.Length>4?rawcode[..4]:rawcode;
}

public sealed class RuntimeSleepBuff : Buff
{
    public override bool BlocksWebAutoCast=>true;
    bool _sleepApplied;

    public override bool Positive=>false;
    public override bool SpellStealable=>false;
    public override bool ExpirationRunsWhilePaused=>true;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Sleep")??"Sleep";
    // 睡眠 Zzz 应挂在头顶(overhead), 而非默认 origin(脚下)。
    public override string VisualAttachmentPoint=>"overhead";

    public void RefreshFrom(RuntimeSleepAbility source,float duration)
    {
        // 502cb0 -> 514320 retains the longer remaining timer, updates the
        // source reference and refreshes the existing sleep object in place.
        Duration=duration<=0?0:Math.Max(Permanent?0:RemainingSeconds,duration);
        Permanent=duration<=0;
        Caster=source.Owner;
        SourceAbility=source;
        RefreshExpiration();
    }

    public override void ModifyIncomingDamage(ref DamageData damage)
    {
        // Native 4e3170 tests the nonzero incoming packet, before armor/final
        // reduction. Remove this buff's state only; other state owners survive.
        if(Owner.GetAbility<RuntimeSleepProtectionBuff>()==null
            &&(damage.Amount+damage.BonusAmount)*damage.DamageMultiplier!=0)
            Owner.RemoveAbility(this);
    }

    protected override void OnBuffAdd()
    {
        Owner.AddState(StateModType.Sleep);
        Owner.Orders.InterruptCurrentCast();
        Owner.Abilities.CancelChannel();
        Owner.AddState(StateModType.InternalPause);
        _sleepApplied=true;
    }

    protected override void OnBuffRemove()
    {
        if(_sleepApplied)
        {
            Owner.RemoveState(StateModType.Sleep);
            Owner.RemoveState(StateModType.InternalPause);
            _sleepApplied=false;
            Owner.Health.NotifySleepRemoval(Caster);
        }
    }

    public override void OnTick(float dt)
    {
        if(_sleepApplied&&!Owner.State.Sleeping)
        {
            Owner.RemoveAbility(this);
            return;
        }
        base.OnTick(dt);
    }
}

/// <summary>CBuffSleepPause: independent BUsp timer, invulnerability and pause.</summary>
public sealed class RuntimeSleepProtectionBuff : Buff
{
    bool _applied;
    bool _pauseApplied;
    public override bool Positive=>false;
    public override bool Magic=>false;
    public override bool SpellStealable=>false;
    public override bool ExpirationRunsWhilePaused=>true;
    public override string Name=>ObjectData?.GetString("Bufftip","Sleep protection")??"Sleep protection";
    public void RefreshFrom(RuntimeSleepAbility source,float duration)
    {
        Duration=duration<=0?duration:Math.Max(Permanent?0:RemainingSeconds,duration);
        Permanent=duration<=0;
        Caster=source.Owner;
        SourceAbility=source;
        RefreshExpiration();
    }
    protected override void OnBuffAdd()
    {
        Owner.World.MovementBatch.Cancel(Owner,null);
        Owner.Orders.InterruptCurrentCast();
        Owner.Abilities.CancelChannel();
        Owner.AddState(StateModType.Invulnerable);
        // 512b00 marks an illusion caster with 0x100; 513d50 then skips
        // acquiring unit +54. Refresh only updates the caster reference.
        if(Caster?.IsIllusion!=true)
        {
            Owner.AddState(StateModType.InternalPause);
            _pauseApplied=true;
        }
        _applied=true;
    }
    protected override void OnBuffRemove()
    {
        if(!_applied)return;
        Owner.RemoveState(StateModType.Invulnerable);
        if(_pauseApplied)Owner.RemoveState(StateModType.InternalPause);
        _pauseApplied=false;
        _applied=false;
    }
}
