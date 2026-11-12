namespace OpenWC3.Game;

/// <summary>Mana Shield（ANms）：切换型护盾，以 DataA 伤害/法力效率吸收 DataB 比例终伤。</summary>
public sealed class RuntimeManaShieldAbility(War3ObjectData objects) : Ability
{
    RuntimeManaShieldBuff _activeBuff;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;
    public float DamagePerMana { get; private set; }
    public float DamageAbsorbedFactor { get; private set; }
    public string ShieldBuffRawcode { get; private set; }="";
    public bool Active=>_activeBuff?.Owner==Owner
        &&Owner?.Abilities.Contains(_activeBuff)==true;
    public override int ResolveCastOrderId(int requested)=>requested!=0?requested:Active?852590:852589;
    public override bool CheckCastOrder(int order)=>order==852589&&!Active||order==852590&&Active;

    public override bool CheckCanUse(Unit target,Vec2 point) => Active
        ? Owner.Alive&&!Owner.IsIllusion&&!IsDisabled&&target==null
        : base.CheckCanUse(target,point);
    public override bool CheckCanUse(Unit target,Vec2 point,int orderId) => orderId==852590
        ? CheckCastOrder(orderId)&&CheckCanUse(target,point)
        : base.CheckCanUse(target,point);
    public override bool CheckBeforeQueue(Unit target,Vec2 point,int orderId)
    {
        if(orderId!=852590)return true;
        if(CheckCanUse(target,point,orderId))Deactivate();
        return false; // 3e0e30 skips the ordinary cast predicate for this direction.
    }
    void Deactivate()
    {
        var buff=_activeBuff;_activeBuff=null;
        if(buff!=null&&Owner?.Abilities.Contains(buff)==true)Owner.RemoveAbility(buff);
    }

    public void RefreshManaShieldData()
    {
        int level=Math.Max(1,Level);
        DamagePerMana=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0));
        DamageAbsorbedFactor=Math.Clamp(RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0),0,1);
        ShieldBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level).SplitConfig(',',
            StringSplitOptions.RemoveEmptyEntries
            |StringSplitOptions.TrimEntries).FirstOrDefault()??"";
        if(ShieldBuffRawcode.Length>4)
            ShieldBuffRawcode=ShieldBuffRawcode[..4];
        if(Active)_activeBuff.UpdateValues(
            DamagePerMana,DamageAbsorbedFactor);
    }

    public override void OnLevelUp()=>RefreshManaShieldData();
    public override void OnLevelDown()=>RefreshManaShieldData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null;
    public override void OnEffect(Unit target,Vec2 point)
    {
        if(Active)
        {
            Deactivate();
            return;
        }
        _activeBuff=new RuntimeManaShieldBuff(
            DamagePerMana,DamageAbsorbedFactor);
        BuffTypeData data=objects?.GetBuff(ShieldBuffRawcode);
        if(data!=null)_activeBuff.BindBuffObjectData(ShieldBuffRawcode,
            data,Math.Max(1,Level),Owner,this);
        else
        {
            _activeBuff.BindObjectData(ShieldBuffRawcode,ObjectData,
                Math.Max(1,Level));
            // 物编缺 buff 行时也要留住来源引用: OnBuffRemove 靠它复位激活态。
            _activeBuff.SourceAbility=this;
        }
        // 物编 ANms 的 Dur1..3 与 HeroDur1..3 全部为 0, 原版语义是**永久** buff
        // (Buff.ApplyDuration 的 0 值分支 → Permanent)。旧实现写死
        // Duration = 1_000_000f 来伪造永久, 两个副作用:
        //   1) 偏离原版: 严格说 1e6 秒后 buff 会自己到期;
        //   2) 视觉时间轴精度塌陷: Buff.VisualAgeSeconds 旧实现用
        //      Duration - RemainingSeconds 反推, 在 1e6 量级上 float 只剩
        //      0.0625s 分辨率, age 退化为 0/0.0625/0.125/0/0/0.125 的不均匀
        //      步进, ManaShieldCaster 的 1000ms 循环序列因此只剩约 8 个
        //      有效帧位 —— 这就是娜迦魔法盾动画卡顿的根因。
        // 改回物编驱动后 Dur=0 走 Permanent; 地图把 Dur 改成有限值时
        // buff 也会按时到期, 与原版一致。
        int durationLevel=Math.Max(1,Level);
        _activeBuff.ApplyDuration(
            RuntimeAbilityFactory.ReadFloat(ObjectData,"Dur",durationLevel,0),
            RuntimeAbilityFactory.ReadFloat(ObjectData,"HeroDur",durationLevel,0));
        Owner.AddAbility(_activeBuff);
    }
    public override void OnRemove()=>Deactivate();

    // Buff 到期 / 被驱散后复位能力侧激活态。原版 CBuffManaShield 与
    // CAbilityManaShield 的开关是同一份状态; 不复位会让命令卡停在
    // "关闭护盾"而实际已经没有任何护盾。
    internal void OnBuffExpired(RuntimeManaShieldBuff buff)
    {
        if(ReferenceEquals(_activeBuff,buff))_activeBuff=null;
    }
}

public sealed class RuntimeManaShieldBuff(
    float damagePerMana,float damageAbsorbedFactor) : Buff
{
    public override bool Magic=>false; // CBuffManaShield +1d8=620d30.
    public override bool AutoDispel=>false;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Mana Shield")??"Mana Shield";
    public void UpdateValues(float efficiency,float factor)
    {
        damagePerMana=efficiency;
        damageAbsorbedFactor=factor;
    }
    protected override void OnBuffAdd() { }
    protected override void OnBuffRemove()
    {
        // 到期 / 被驱散 / 法力耗尽主动移除, 都要把能力侧开关一起复位。
        if(SourceAbility is RuntimeManaShieldAbility shield)shield.OnBuffExpired(this);
    }
    public override float ModifyFinalDamage(float trueDamage,
        ref DamageData damage)
    {
        // CBuffManaShield +fc=604790 requires a resolved source. A positive
        // shield reaction removes the Buff if it cannot absorb or drains mana.
        if(damage.Source==null||trueDamage<=0||damagePerMana<=0
            ||damageAbsorbedFactor<=0)return trueDamage;
        float desired=trueDamage*damageAbsorbedFactor;
        float absorbed=Math.Min(desired,Owner.Mana*damagePerMana);
        if(absorbed<=0){Owner.RemoveAbility(this);return trueDamage;}
        Owner.Mana=Math.Max(0,Owner.Mana-absorbed/damagePerMana);
        if(Owner.Mana<=0)Owner.RemoveAbility(this);
        return trueDamage-absorbed;
    }
}
