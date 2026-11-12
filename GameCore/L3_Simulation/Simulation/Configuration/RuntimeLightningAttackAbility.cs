namespace OpenWC3.Game;

/// <summary>Alit: attack-bound zero-damage lightning visual, not Forked Lightning.</summary>
public sealed class RuntimeLightningAttackAbility : Ability
{
    ChainLightningBeamEffect _beam;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
    public override void OnAttackLaunched(Unit target,Weapon weapon,int rolledDamage)
    {
        if(IsDisabled||Level<1||Owner?.Alive!=true||Owner.Removed||target==null
            ||!target.CanBeTargetedBy(Owner,ConfiguredTargets,useHeroSpellRules:true))return;
        ClearBeam();
        float duration=Math.Max(0,GetLevelFloat("DataB"));
        if(duration<=0)return;
        if(LightningEffects.Count==0)return;
        _beam=new ChainLightningBeamEffect(Owner,target,duration,LightningEffects[0]);
        Owner.World.AddEffect(_beam);
    }
    void ClearBeam(){_beam?.Cancel();_beam=null;}
    public override void OnRemove()=>ClearBeam();
    public override void OnDeath()=>ClearBeam();
}
