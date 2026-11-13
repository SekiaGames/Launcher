namespace OpenWC3.Game;

/// <summary>
/// Invisibility（Aivs）：目标在 DataA 淡入后隐身，动作会解除。
/// 原版 CBuffInvisibility 在隐身来源生效时（事件 0xd01de）由 6f6256a0 ->
/// 6f60b2e0 给 CAbilityAttack+220 增加一份"禁止自动索敌"计数并打断当前攻击，
/// 因此隐身期间不会自动开火；玩家主动攻击才由 6f626880（虚表 +12c）移除
/// 该 Buff 并显形。移除路径 6f613da0 通过 6f478020 撤销同一份计数。
/// </summary>
public sealed class RuntimeInvisibilityAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Unit;
    public float TransitionTime { get; private set; }
    public float NormalDuration { get; private set; }
    public float HeroDuration { get; private set; }
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshInvisibilityData()
    {
        int level=Math.Max(1,Level);
        TransitionTime=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0));
        NormalDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        HeroDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"HeroDur",level,NormalDuration));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Player|TargetFlags.Ally|TargetFlags.Self|TargetFlags.Organic
                |TargetFlags.Alive|TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshInvisibilityData();
    public override void OnLevelDown()=>RefreshInvisibilityData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>
        target?.Alive==true&&NativeSpellTargetRules.Matches(this,target)&&target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    public override bool CheckBeforeQueue(Unit target,Vec2 point)=>CheckTarget(target,point);

    public override void OnEffect(Unit target,Vec2 point)
    {
        if(!CheckTarget(target,point))return;
        float duration=target.IsHero?HeroDuration:NormalDuration;
        RuntimeInvisibilityBuff existing=target.GetAbility<RuntimeInvisibilityBuff>();
        if(existing!=null)
        {
            existing.Caster=Owner;existing.SourceAbility=this;
            existing.RefreshDuration(duration);
            return;
        }
        var buff=new RuntimeInvisibilityBuff(TransitionTime)
            {Caster=Owner,SourceAbility=this};
        string rawcode=BuffRawcodes.FirstOrDefault()??"";
        BuffTypeData data=objects?.GetBuff(rawcode);
        if(data!=null)buff.BindBuffObjectData(rawcode,data,Math.Max(1,Level),
            Owner,this);
        else buff.BindObjectData(rawcode,ObjectData,Math.Max(1,Level));
        buff.Duration=duration;
        buff.Permanent=duration<=0;
        target.AddAbility(buff);
    }
}

public sealed class RuntimeInvisibilityBuff(float transitionTime) : Buff
{
    float _transitionRemaining;
    bool _invisible;
    bool _autoAttackSuppressed;

    public override string Name=>ObjectData?.GetString(
        "Bufftip","Invisibility")??"Invisibility";
    public float TransitionTime=>transitionTime;

    public void RefreshDuration(float duration)
    {
        // 611920 uses timed-buff refresh 514320 for an existing Binv. It
        // extends expiry without removing the unit's invisibility contribution.
        Duration=duration<=0?0:Math.Max(duration,Permanent?0:RemainingSeconds);
        Permanent=duration<=0;
        RefreshExpiration();
    }

    protected override void OnBuffAdd()
    {
        _transitionRemaining=Math.Max(0,transitionTime);
        // 6f61f710 在交给单位淡入服务 64bfb0 之前先广播 0xd01de；CBuffInvisibility
        // 的 6f6256a0 -> 6f60b2e0 要求自身 +20 的 100 位已置，随后给
        // CAbilityAttack+220 加一、并用 47fb50 取消当前一次攻击。该计数从淡入
        // 开始就生效（不是等淡入结束），因此在这里授予。
        SuppressAutoAttack();
        if(_transitionRemaining<=0)ApplyInvisibility();
    }

    protected override void OnBuffRemove()
    {
        // 6f613da0 先撤销隐身来源与 6773f0 淡入，再对仍持有的 220 计数调用
        // 478020 撤销一份；攻击破隐（6f626880 移除 Buff）也走这里。
        ReleaseAutoAttackSuppression();
        if(!_invisible)return;
        Owner.RemoveState(StateModType.Invisible);
        _invisible=false;
    }

    public override void OnTick(float dt)
    {
        base.OnTick(dt);
        if(Owner==null||!Owner.Abilities.Contains(this)||_invisible)return;
        _transitionRemaining-=Math.Max(0,dt);
        if(_transitionRemaining<=World.TickSeconds*.5f)ApplyInvisibility();
    }

    public override void OnOwnerActionStarted()
    {
        if(Owner?.Abilities.Contains(this)==true)Owner.RemoveAbility(this);
    }

    void ApplyInvisibility()
    {
        if(_invisible)return;
        Owner.AddState(StateModType.Invisible);
        _invisible=true;
    }

    void SuppressAutoAttack()
    {
        // 原版只在单位存在 1e8 攻击组件时占用计数；没有攻击组件不会产生状态。
        if(_autoAttackSuppressed||Owner?.Attack==null)return;
        Owner.AddState(StateModType.DisableAutoAttack);
        _autoAttackSuppressed=true;
    }

    void ReleaseAutoAttackSuppression()
    {
        if(!_autoAttackSuppressed)return;
        _autoAttackSuppressed=false;
        Owner?.RemoveState(StateModType.DisableAutoAttack);
    }
}
