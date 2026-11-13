namespace OpenWC3.Game;

/// <summary>CAbilityMagicImmunity（Amim）：常驻魔法免疫的被动计数来源。</summary>
public sealed class RuntimeMagicImmunityAbility : Ability
{
    bool _applied;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;

    void RefreshMagicImmunity()
    {
        bool shouldApply=Owner!=null&&Level>0;
        if(shouldApply==_applied)return;
        if(shouldApply)
        {
            // 61ee00 -> 46e150 commits the reference before removing buffs.
            _applied=true;
            Owner.AddState(StateModType.MagicImmune);
            RemoveImmunityEntryBuffs(Owner);
            return;
        }
        else Owner?.RemoveState(StateModType.MagicImmune);
        _applied=shouldApply;
    }
    // Shared native 46e150 cleanup, also used by CBuffAvatar's forced entry.
    internal static void RemoveImmunityEntryBuffs(Unit owner)
    {
        // The native special-case lookup removes one CBuffPurge even
        // though that class is nonmagic; custom BuffID does not change it.
        var purge=owner.Abilities.OfType<RuntimePurgeBuff>().FirstOrDefault();
        if(purge!=null)owner.RemoveAbility(purge);
        // 4713d0(0,1,0,0,0,1,1,0,1): both positive and negative magic
        // buffs, excluding aura and timed life; AutoDispel is not required.
        foreach(Buff buff in owner.Abilities.OfType<Buff>()
            .Where(buff=>buff.Magic&&!buff.Aura&&!buff.TimedLife).ToArray())
            if(owner.Abilities.Items.Contains(buff))owner.RemoveAbility(buff);
    }
    public override void OnAdd()=>RefreshMagicImmunity();
    public override void OnSpawn()=>RefreshMagicImmunity();
    public override void OnLevelUp()=>RefreshMagicImmunity();
    public override void OnLevelDown()=>RefreshMagicImmunity();
    public override void OnRemove()
    {
        if(_applied)Owner?.RemoveState(StateModType.MagicImmune);
        _applied=false;
    }
}
