namespace OpenWC3;

// 命令/施法目标形态。不能靠 Point==(0,0) 推断，地图原点本身也是合法目标点。
public enum EventTargetKind
{
    None,
    Point,
    Unit,
    Item,
    Destructable,
}

// 事件上下文 (对标 TriggerExecutionScope: 携带类型化事件数据; 可变以支持 DAMAGING 修改)
public sealed class EventContext
{
    public GameEvent Event;
    public Unit Source;          // 攻击者/施法者/下令者
    public Unit Target;          // 受体/被攻击者/法术目标；不一定是 GetTriggerUnit
    public Unit TriggerUnit;     // 显式触发单位；施法事件是施法者而不是法术目标
    public int TriggerPlayerId = -1;
    public float Amount;         // 伤害量 (UnitDamaging 时 handler 可修改)
    public bool IsAttack;
    public bool IsRanged;
    public bool IsPhysical;
    // YDWE can replace the final amount during the legacy damaged event.
    public bool DamageAmountModified;
    EventContext _damageDispatch;
    long _damageVersion;
    static long _nextDamageVersion;
    bool _damageActive,_damageLifeApplied;
    public bool HasActiveDamageDispatch=>_damageDispatch?._damageActive==true
        &&_damageDispatch._damageVersion==_damageVersion;
    public void BeginDamageDispatch(bool lifeApplied)
    {
        _damageDispatch=this;
        _damageVersion=unchecked(++_nextDamageVersion);
        _damageLifeApplied=lifeApplied;
        _damageActive=true;
    }
    public void EndDamageDispatch()=>_damageActive=false;
    public bool TryReplaceEventDamage(float amount)
    {
        if(!HasActiveDamageDispatch||!float.IsFinite(amount))return false;
        EventContext dispatch=_damageDispatch;
        // Reconcile immediately so a nested damage call sees the corrected HP.
        // A sleeping trigger holds a snapshot, whose dispatch is no longer active.
        if(dispatch._damageLifeApplied&&dispatch.Target!=null)
            dispatch.Target.Life+=dispatch.Amount-amount;
        dispatch.Amount=Amount=amount;
        dispatch.DamageAmountModified=DamageAmountModified=true;
        return true;
    }
    public bool TryReplaceEventDamageTypes(AttackType? attack=null,DamageType? damage=null,WeaponSound? weapon=null)
    {
        if(!HasActiveDamageDispatch)return false;
        EventContext dispatch=_damageDispatch;
        if(attack.HasValue)dispatch.AttackType=AttackType=attack.Value;
        if(damage.HasValue)
        {dispatch.DamageType=DamageType=damage.Value;dispatch.NativeDamageTypeMask=NativeDamageTypeMask=null;}
        if(weapon.HasValue)dispatch.WeaponSound=WeaponSound=weapon.Value;
        return true;
    }
    public bool SuppressKillBounty;
    // 原版 67ead0：单位仍有待完成的 CEffectReincarnation（XOre）时，d0153
    // 只结算经验/击杀，不派发 0x80214/0x80235。引擎内部监听器仍需要死亡
    // 事件（选择集、死亡表现、掉落、赏金），因此由 JASS 派发端检查此标志。
    public bool SuppressScriptEvent;
    public AttackType AttackType;
    public DamageType DamageType;
    // Notify-only native packets have a zero mask, distinct from Unknown (bit zero).
    public uint? NativeDamageTypeMask;
    public WeaponSound WeaponSound;   // 武器声音类型 (对标 unitDamageEvent 的 weaponSoundType)
    public Ability Ability;      // 施法事件的技能
    public Vec2 Point;           // 施法目标点
    public string AbilityId => Ability?.Rawcode ?? "";
    public string OrderId;
    public int OrderIdValue;
    public EventTargetKind TargetKind;
    public Unit OrderTargetUnit;
    public object OrderTargetItem;
    public object OrderTargetDestructable;
    public object SpellTargetItem;
    public object SpellTargetDestructable;
    public object TriggerWidget;
    public object TriggerDestructable;
    public object ClickedDialog;
    public object ClickedButton;
    public object ManipulatedItem;
    public Unit SellingUnit;
    public Unit BuyingUnit;
    public Unit SoldUnit;
    public object SoldItem;
    public Unit SummonedUnit;
    public Unit SummoningUnit;
    public Unit ConstructingStructure;
    public Unit ConstructedStructure;
    public Unit CancelledStructure;
    public Unit TrainedUnit;
    public int TrainedUnitTypeId;
    public Unit ResearchingUnit;
    public int ResearchedId;
    public Unit TransportUnit;
    public Unit LoadedUnit;
    public int LearnedSkillId;
    public int LearnedSkillLevel;
    public int PlayerStateId = -1;
    public int UnitStateId = -1;
    public int GameStateId = -1;
    public int PreviousOwnerId = -1;
    public int AllianceTypeId = -1;
    public string ChatString = "";
    public string ChatStringMatched = "";
    public int JassEventId = -1;
    public Unit Killer;          // UnitDies 的击杀者
    public bool Cancelled;       // handler 可取消 (如吸收/免疫)
    // ---- 区域/轨迹体事件字段 (对标 GetTriggeringRegion/GetTriggeringTrackable) ----
    // Region 事件携带具体 region 引用 (进入/离开时哪个 region 被触发);
    // Trackable 事件携带被命中/追踪的 trackable 引用; TrackableRef 在 OpenWC3.Game
    // 定义, 此处仅用 object 避免环形引用; 原生 getter 内部 cast。
    public object TriggerRegion;
    public object TriggerTrackable;
    public object TriggerFrame;
    public int FrameEventId;
    public float FrameValue;
    public string FrameText="";
    public string SyncPrefix="",SyncData="";
    public int MouseButtonId,KeyId,MetaKey;
    public bool KeyDown;
    /// <summary>
    /// 为 true 时消费方不播升级特效与音效。JASS showEyeCandy=false 转为此标志，
    /// 升级事件照常分发；自然经验升级默认显示表现。
    /// </summary>
    public bool SuppressLevelUpVisual;

    // ---- 池化 ----
    // EventBus.Fire 同步派发, JASS 侧 _contexts 只在触发器执行期消费(OrderQueue 已有
    // 同款复用先例); Snapshot() 的 MemberwiseClone 是独立对象, 不参与池。
    // 复用方必须在最后一次读取后调用 Recycle; 中途异常只导致本实例不回池, 无污染。
    static readonly System.Collections.Generic.Stack<EventContext> Pool = new();
    public static EventContext Rent()
    {
        if (Pool.TryPop(out EventContext context)) { context.Reset(); return context; }
        return new EventContext();
    }
    public static void Recycle(EventContext context)
    {
        if (Pool.Count < 64) Pool.Push(context);
    }
    void Reset()
    {
        Event=default;Source=null;Target=null;TriggerUnit=null;TriggerPlayerId=-1;
        Amount=0;IsAttack=false;IsRanged=false;IsPhysical=false;DamageAmountModified=false;SuppressKillBounty=false;SuppressScriptEvent=false;AttackType=default;
        _damageDispatch=null;_damageActive=false;_damageLifeApplied=false;_damageVersion=0;
        NativeDamageTypeMask=null;DamageType=default;WeaponSound=default;Ability=null;Point=default;
        OrderId=null;OrderIdValue=0;TargetKind=default;OrderTargetUnit=null;
        OrderTargetItem=null;OrderTargetDestructable=null;SpellTargetItem=null;
        SpellTargetDestructable=null;TriggerWidget=null;TriggerDestructable=null;
        ClickedDialog=null;ClickedButton=null;ManipulatedItem=null;SellingUnit=null;
        BuyingUnit=null;SoldUnit=null;SoldItem=null;SummonedUnit=null;
        SummoningUnit=null;ConstructingStructure=null;ConstructedStructure=null;
        CancelledStructure=null;TrainedUnit=null;TrainedUnitTypeId=0;
        ResearchingUnit=null;ResearchedId=0;TransportUnit=null;LoadedUnit=null;
        LearnedSkillId=0;LearnedSkillLevel=0;PlayerStateId=-1;UnitStateId=-1;GameStateId=-1;
        PreviousOwnerId=-1;AllianceTypeId=-1;ChatString="";ChatStringMatched="";
        JassEventId=-1;Killer=null;Cancelled=false;TriggerRegion=null;
        TriggerTrackable=null;SuppressLevelUpVisual=false;
        TriggerFrame=null;FrameEventId=0;FrameValue=0;FrameText="";SyncPrefix="";SyncData="";
        MouseButtonId=0;KeyId=0;MetaKey=0;KeyDown=false;
    }

    /// <summary>
    /// JASS sleeping thread 必须冻结事件响应字段；单位、物品等引擎对象仍共享同一实体。
    /// </summary>
    public EventContext Snapshot() => (EventContext)MemberwiseClone();

    // ---- Trigger 条件函数 (对标 JASS natives, 在事件 handler 内从上下文取值) ----
    public Unit GetTriggerUnit() => TriggerUnit ?? Target ?? Source;
    public Unit GetAttacker() => Source;                         // GetAttacker: 攻击者
    public Unit GetKillingUnit() => Killer ?? Source;           // GetKillingUnit: 击杀者
    public Unit GetEventDamageSource() => Source;               // GetEventDamageSource
    public float GetEventDamage() => Amount;                    // GetEventDamage: 伤害量
    public Ability GetSpellAbility() => Ability;                // GetSpellAbilityId/Unit 的技能对象
    // GetUnitState(unit, state): 取单位当前生命/最大生命/法力/最大法力 (对标 GetUnitState)
    public enum State { Life, MaxLife, Mana, MaxMana }
    public static float GetUnitState(Unit u, State s) => s switch
    {
        State.Life => u.Life, State.MaxLife => u.MaxLife,
        State.Mana => u.Mana, _ => u.MaxMana,
    };
}
