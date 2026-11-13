namespace OpenWC3.Game;

/// <summary>CAbilityDivineShield（AHds）：无需选择目标，为施法者添加限时无敌 Buff。
/// 原版效果 FUN_6f5baff0：英雄 HeroDur/普通 Dur，按 'BHds' 查已有 buff 原地
/// Refresh（只延长不缩短，新时长<=0 转永久），并移除身上的 Purge(Bprg)。</summary>
public sealed class RuntimeDivineShieldAbility(War3ObjectData objects) : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;
    RuntimeDivineShieldBuff _shield;
    public bool Active => _shield != null && Owner?.Abilities.Contains(_shield) == true;
    public bool CanDeactivate => RuntimeAbilityFactory.ReadFloat(ObjectData,"DataA",Math.Max(1,Level),0)!=0
        && Owner?.World?.GameplayConfig?.CanDeactivateDivineShield == true;
    public override int ResolveCastOrderId(int requested) => requested!=0?requested:
        Active && CanDeactivate?852091:852090;
    public override bool CheckCastOrder(int orderId) => orderId==852090
        || orderId==852091 && Active && CanDeactivate;
    public override bool CheckCanUse(Unit target,Vec2 point) => Active && CanDeactivate
        ? Owner.Alive && !Owner.IsIllusion && !IsDisabled && target==null
        : base.CheckCanUse(target,point);
    public override bool CheckCanUse(Unit target,Vec2 point,int orderId) => orderId==852091
        ? CheckCastOrder(orderId) && CheckCanUse(target,point)
        : base.CheckCanUse(target,point);
    public override bool CheckBeforeQueue(Unit target,Vec2 point,int orderId)
    {
        if(orderId!=852091)return true;
        if(CheckCanUse(target,point,orderId))RemoveShield();
        return false; // Native bound-buff cleanup does not start another spell cast.
    }
    void RemoveShield()
    {
        var shield=_shield;_shield=null;
        if(shield!=null && Owner?.Abilities.Contains(shield)==true)Owner.RemoveAbility(shield);
    }
    public override void OnRemove()=>RemoveShield();
    public float NormalDuration { get; private set; }
    public float HeroDuration { get; private set; }
    public string DivineShieldBuffRawcode { get; private set; }="";

    public void RefreshDivineShieldData()
    {
        int level=Math.Max(1,Level);
        NormalDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        HeroDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"HeroDur",level,NormalDuration));
        DivineShieldBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(DivineShieldBuffRawcode.Length>4)
            DivineShieldBuffRawcode=DivineShieldBuffRawcode[..4];
    }

    public override void OnLevelUp()=>RefreshDivineShieldData();
    public override void OnLevelDown()=>RefreshDivineShieldData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null;

    public override void OnEffect(Unit target,Vec2 point)
    {
        if(Owner==null)return;
        // 原版 Init 0x6f5ca500 在获得无敌前主动移除 Purge。
        foreach(ObjectDataBuff purge in Owner.Abilities.OfType<ObjectDataBuff>()
                    .Where(buff=>buff.Rawcode.Equals("Bprg",
                        StringComparison.OrdinalIgnoreCase)).ToArray())
            Owner.RemoveAbility(purge);
        // 原版按类内硬编码 'BHds' 查找; BuffID 缺省即 BHds。
        string buffId=DivineShieldBuffRawcode is {Length:>=4} id?id[..4]:"BHds";
        var existing=Owner.Abilities.OfType<RuntimeDivineShieldBuff>()
            .FirstOrDefault(buff=>buff.Rawcode.Equals(buffId,
                StringComparison.OrdinalIgnoreCase));
        if(existing!=null)
        {
            existing.RefreshShield(Owner.UsesHeroSpellRules
                ?HeroDuration:NormalDuration);
            return;
        }
        var buff=new RuntimeDivineShieldBuff();
        BuffTypeData data=objects?.GetBuff(buffId);
        if(data!=null)buff.BindBuffObjectData(buffId,data,
            Math.Max(1,Level),Owner,this);
        else buff.BindObjectData(buffId,ObjectData,
            Math.Max(1,Level));
        buff.ApplyDuration(NormalDuration,HeroDuration);
        _shield=buff;
        Owner.AddAbility(buff);
    }
}

public sealed class RuntimeDivineShieldBuff : Buff
{
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Divine Shield")??"Divine Shield";
    protected override void OnBuffAdd()=>Owner.AddState(StateModType.Invulnerable);
    protected override void OnBuffRemove()=>Owner.RemoveState(StateModType.Invulnerable);

    /// <summary>CBuff::Refresh 0x6f514320：新时长<=0 转永久；否则只延长不缩短。</summary>
    public void RefreshShield(float newDuration)
    {
        if(newDuration<=0)
        {
            Permanent=true;
            Duration=0;
            RefreshExpiration();
            return;
        }
        if(Permanent||RemainingSeconds>=newDuration)return;
        Duration=newDuration;
        RefreshExpiration();
    }
}
