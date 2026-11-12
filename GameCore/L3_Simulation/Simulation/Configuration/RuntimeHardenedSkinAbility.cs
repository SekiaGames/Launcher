namespace OpenWC3.Game;

/// <summary>Assk / CAbilityStoneSkin: priority 5, before armor and type scaling.</summary>
public sealed class RuntimeHardenedSkinAbility : Ability
{
    // Original 5e3210 table; exact float bits captured by stone_skin_native.py.
    static readonly uint[] ProbabilityBits={0,0x3b79096c,0x3c70d845,0x3d03126f,0x3d600d1c,
        0x3da7bb30,0x3de69ad4,0x3e159b3d,0x3e39db23,0x3e5fa440,0x3e832ca5,
        0x3e973190,0x3eaaa64c,0x3ec19653,0x3ed73190,0x3eebe0df,0x3f000000,
        0x3f147454,0x3f2aa64c,0x3f45460b,0x3f800000};
    int _attempt=1;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override int IncomingDamagePriority=>5; // StoneSkin +f8 508080.
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
    public override void OnAdd()=>_attempt=1;
    public override void ModifyIncomingDamage(ref DamageData damage)=>ApplySkin(ref damage);
    public override bool ApplyIncomingDamage(ref DamageData damage)=>ApplySkin(ref damage);
    bool ApplySkin(ref DamageData damage)
    {
        if(IsDisabled||Level<1||damage.Source==null||damage.DamageType!=DamageType.Normal
            ||Owner?.Alive!=true||Owner.Removed||Owner.IsIllusion
            ||!damage.Source.CanBeTargetedBy(Owner,ConfiguredTargets,ignoreVisibility:true,useHeroSpellRules:true))return false;
        if(GetLevelFloat(damage.IsRanged?"DataD":"DataE")==0)return false;
        float amount=(damage.Amount+damage.BonusAmount)*damage.DamageMultiplier;
        if(amount<=0)return false;
        int index=Math.Clamp((int)(Math.Clamp(GetLevelFloat("DataA"),0,100)*.2f+.5f),0,20);
        float probability=BitConverter.Int32BitsToSingle(unchecked((int)ProbabilityBits[index]))*_attempt;
        float roll=Owner.World.HardenedSkinRng.NextUnit();
        if(roll>probability){if(_attempt<int.MaxValue)_attempt++;return false;}
        _attempt=1;
        float reduction=GetLevelFloat("DataC");
        if(reduction==0)reduction=amount;
        // 697130 has a strict comparison, not max(minimum, amount-reduction).
        damage.Amount=reduction<amount?amount-reduction:GetLevelFloat("DataB");
        damage.BonusAmount=0;damage.DamageMultiplier=1;
        return true; // 4e2fa0: first successful skin ends priority 5.
    }
}
