namespace OpenWC3.Game;

/// <summary>Agho: native CAbilityGhost inherits permanent invisibility, unlike Aeth.</summary>
public sealed class RuntimeInvisibleGhostAbility : RuntimePermanentInvisibilityAbility
{
    bool _unitCollisionApplied, _buildingCollisionApplied;
    public override bool IsPassive=>true;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;

    public override void OnAdd(){base.OnAdd();RefreshCollision();}
    public override void OnSpawn(){base.OnSpawn();RefreshCollision();}
    public override void OnLevelUp(){base.OnLevelUp();RefreshCollision();}
    public override void OnLevelDown(){base.OnLevelDown();RefreshCollision();}
    public override void OnDisabledStateChanged(){base.OnDisabledStateChanged();RefreshCollision();}
    public override void OnDeath(){base.OnDeath();ClearCollision();}
    public override void OnRemove(){base.OnRemove();ClearCollision();}

    void RefreshCollision()
    {
        // 62cb50 -> 6064a0: unit collision follows availability; building
        // collision uses DataB (Aeth instead reads DataA at this call site).
        bool active=Owner?.Alive==true&&Level>0&&!IsDisabled&&!IsRuntimeLocked;
        SetCollision(StateModType.DisableUnitCollision,active,ref _unitCollisionApplied);
        SetCollision(StateModType.DisableBuildingCollision,active&&RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",Math.Max(1,Level),0)!=0,ref _buildingCollisionApplied);
    }
    void ClearCollision()
    {
        SetCollision(StateModType.DisableUnitCollision,false,ref _unitCollisionApplied);
        SetCollision(StateModType.DisableBuildingCollision,false,ref _buildingCollisionApplied);
    }
    void SetCollision(StateModType state,bool enabled,ref bool applied)
    {
        if(enabled==applied)return;
        if(enabled)Owner.AddState(state);else Owner?.RemoveState(state);
        applied=enabled;
    }
}
