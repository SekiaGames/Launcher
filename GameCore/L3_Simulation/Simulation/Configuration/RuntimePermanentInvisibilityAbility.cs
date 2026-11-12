namespace OpenWC3.Game;

/// <summary>
/// Permanent Invisibility（Apiv）：静止到 Dur 后进入隐身；攻击或法术生效时
/// 只撤销本能力贡献的 Invisible 状态，并重新开始淡入计时。
/// </summary>
public class RuntimePermanentInvisibilityAbility : Ability
{
    float _fadeRemaining;
    bool _invisibilityApplied;
    bool _autoAttackSuppressed;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
    public float FadeDuration { get; private set; }
    public bool AutoAcquireAttackTargets { get; private set; }

    public void RefreshPermanentInvisibilityData()
    {
        int level=Math.Max(1,Level);
        FadeDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        AutoAcquireAttackTargets=StandalonePathing.Core.NativeSoftFloat.Truncate(
            RuntimeAbilityFactory.ReadFloat(ObjectData,"DataA",level,1))!=0;
        ResetFade();
    }

    public override void OnAdd()=>RefreshState();
    public override void OnSpawn()=>RefreshState();

    // Native 3E9FA0 -> vtable+E0 (610ED0) withdraws the source and
    // acquisition flag on the first disable. 3EE3C0 -> +E4 (613C30)
    // starts a fresh fade only after the final disable reason is removed.
    public override void OnDisabledStateChanged()
    {
        if(Owner==null)return;
        ResetFade();
        RefreshState();
    }

    public override void OnLevelUp()
    {
        RefreshPermanentInvisibilityData();
        RefreshState();
    }

    public override void OnLevelDown()
    {
        RefreshPermanentInvisibilityData();
        RefreshState();
    }

    public override void OnOwnerActionStarted()
    {
        if(Level<=0||IsDisabled||IsRuntimeLocked||FadeDuration<=0)return;
        RemoveOwnInvisibility();
        _fadeRemaining=FadeDuration;
    }

    public override void OnTick(float dt)
    {
        base.OnTick(dt);
        if(Level<=0||IsDisabled||IsRuntimeLocked)
        {
            RemoveOwnInvisibility();
            return;
        }
        if(_invisibilityApplied)return;
        _fadeRemaining=Math.Max(0,_fadeRemaining-Math.Max(0,dt));
        // Dur 通常正好是 20Hz tick 的整数倍；容忍浮点累计尾差，避免 3 秒
        // 在第 60 tick 仍残留数个 ulp 而晚一帧进入隐身。
        if(_fadeRemaining<=World.TickSeconds*.5f)ApplyOwnInvisibility();
    }

    public override void OnDeath()
    {
        RemoveOwnInvisibility();
        SetAcquisitionSuppression(false);
        base.OnDeath();
    }

    public override void OnRemove()
    {RemoveOwnInvisibility();SetAcquisitionSuppression(false);}

    void RefreshState()
    {
        if(Owner==null)return;
        RefreshAcquisition();
        if(Level<=0||IsDisabled||IsRuntimeLocked)
        {
            RemoveOwnInvisibility();
            return;
        }
        if(FadeDuration<=0)ApplyOwnInvisibility();
        else if(!_invisibilityApplied&&_fadeRemaining<=0)
            _fadeRemaining=FadeDuration;
    }

    void ResetFade()
    {
        RemoveOwnInvisibility();
        _fadeRemaining=Level>0?FadeDuration:0;
        RefreshAcquisition();
    }
    void RefreshAcquisition()=>SetAcquisitionSuppression(
        Owner?.Alive==true&&Owner.Attack!=null&&Level>0&&!IsDisabled&&!IsRuntimeLocked&&!AutoAcquireAttackTargets);
    void SetAcquisitionSuppression(bool suppress)
    {
        if(suppress==_autoAttackSuppressed)return;
        _autoAttackSuppressed=suppress;
        if(suppress)Owner?.AddState(StateModType.DisableAutoAttack);
        else Owner?.RemoveState(StateModType.DisableAutoAttack);
    }

    void ApplyOwnInvisibility()
    {
        if(_invisibilityApplied||Owner==null)return;
        Owner.State.Add(StateModType.Invisible);
        _invisibilityApplied=true;
    }

    void RemoveOwnInvisibility()
    {
        if(!_invisibilityApplied||Owner==null)return;
        Owner.State.Remove(StateModType.Invisible);
        _invisibilityApplied=false;
    }
}
