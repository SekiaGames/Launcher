namespace OpenWC3.Game;

/// <summary>CAbilityInvulnerable（Avul）：存在且有等级时贡献计数式无敌状态。</summary>
public sealed class RuntimeInvulnerableAbility : Ability
{
    bool _applied;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
    public override void OnAdd()=>RefreshInvulnerableState();
    public override void OnSpawn()=>RefreshInvulnerableState();
    public override void OnLevelUp()=>RefreshInvulnerableState();
    public override void OnLevelDown()=>RefreshInvulnerableState();
    public override void OnDeath()
    {
        RemoveOwnState();
        base.OnDeath();
    }
    public override void OnRemove()=>RemoveOwnState();
    public void RefreshInvulnerableState()
    {
        bool shouldApply=Owner!=null&&Level>0;
        if(shouldApply==_applied)return;
        if(shouldApply)Owner.State.Add(StateModType.Invulnerable);
        else Owner.State.Remove(StateModType.Invulnerable);
        _applied=shouldApply;
    }
    void RemoveOwnState()
    {
        if(!_applied||Owner==null)return;
        Owner.State.Remove(StateModType.Invulnerable);
        _applied=false;
    }
}
