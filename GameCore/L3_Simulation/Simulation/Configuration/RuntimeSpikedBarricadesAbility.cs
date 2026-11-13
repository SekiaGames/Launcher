namespace OpenWC3.Game;

/// <summary>CAbilitySpiked (Aspi): fixed DataA damage returned to melee attackers.</summary>
public sealed class RuntimeSpikedBarricadesAbility : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
    float _techReturnedDamageBonus;
    bool _hasSpikeArt;
    public float TechReturnedDamageBonus
    {
        get=>_techReturnedDamageBonus;
        set
        {
            if(_techReturnedDamageBonus==value)return;
            _techReturnedDamageBonus=value;
            RefreshSpikeArt();
        }
    }
    public float ReturnedDamage=>Math.Abs(GetLevelFloat("DataA")+TechReturnedDamageBonus);
    // The stock SpikeBarrier TargetArt surrounds the building only once the
    // ability has a nonzero return amount (normally supplied by Rosp research).
    public override bool HasPersistentTargetArts=>_hasSpikeArt;
    void RefreshSpikeArt()=>_hasSpikeArt=ReturnedDamage>=2e-7f;
    protected override void OnObjectDataBound()=>RefreshSpikeArt();
    public override void OnLevelUp()=>RefreshSpikeArt();
    public override void OnLevelDown()=>RefreshSpikeArt();
    public override void OnDamaged(ref DamageData damage,float dealt)
    {
        var attacker=damage.Source;
        // Original 4e2ed0 tests packet flags/type and source relationship, not
        // actual damage dealt or the ordinary-attack event flag.
        if(IsDisabled||Level<=0||attacker?.Alive!=true||damage.IsRanged
            ||damage.DamageType is not (DamageType.Normal or DamageType.Demolition)
            ||!Owner.World.AreEnemies(Owner,attacker)||ReturnedDamage<2e-7f)return;
        attacker.TakeDamage(new DamageData{Source=Owner,Amount=ReturnedDamage,
            AttackType=AttackType.Spells,DamageType=DamageType.Defensive});
    }
}
