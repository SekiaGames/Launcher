namespace OpenWC3.Game;

/// <summary>CAbilityDemolish（ANde）：命中建筑时按概率应用倍率与固定伤害。</summary>
public sealed class RuntimeDemolishAbility : Ability
{
    float _procPercent;
    int _procAttempts=1;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public float ProcChance { get; private set; }
    public float DamageFactor { get; private set; }
    public float BonusDamage { get; private set; }
    public float DataD { get; private set; }

    public void RefreshDemolishData()
    {
        int level=Math.Max(1,Level);
        _procPercent=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataA",level,0);
        ProcChance=Math.Clamp(_procPercent/100f,0,1);
        DamageFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0));
        BonusDamage=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataC",level,0);
        DataD=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataD",level,0);
    }

    public override void OnLevelUp()=>RefreshDemolishData();
    public override void OnLevelDown()=>RefreshDemolishData();
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
    public override void ModifyOutgoingDamage(Unit target,ref DamageData damage)
    {
        if(Level<=0||IsDisabled||IsRuntimeLocked||!damage.IsAttack
            ||target?.Alive!=true||!target.IsBuilding||Owner?.World==null)return;
        // 5866d0 -> 57b300 shares 5e3210's PRD buckets. Stream 40
        // supplies 675f70; only eligible, available attacks advance attempts.
        float threshold=CriticalStrike.NativeProcThreshold(_procPercent,_procAttempts);
        if(Owner.World.ScriptRandom.Stream(40).NextUnit()>threshold)
        {_procAttempts++;return;}
        _procAttempts=1;
        damage.DamageMultiplier*=DamageFactor;
        damage.BonusAmount+=BonusDamage;
    }
}
