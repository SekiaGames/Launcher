namespace OpenWC3;

// Normal applies numeric armor outside construction. All types, including
// Universal, use the attack/defense-class matrix (Game.dll 67dc40).
// Immunity is independently decided by the native damage mask, attack type
// and Misc configuration. Distinct JASS types retain their event identity.
public enum DamageType
{
    // Preserve the existing serialized values; append distinct JASS types.
    Normal, Magic, Universal, Divine, Fire, Enhanced, Defensive, Demolition,
    Unknown, Cold, Lightning, Poison, Disease, Sonic, Acid, Force, Death,
    Mind, Plant, SlowPoison, SpiritLink, ShadowStrike
}

public static class DamageTypeCodec
{
    public static DamageType FromJassId(int id)=>id switch
    {
        0=>DamageType.Unknown,4=>DamageType.Normal,5=>DamageType.Enhanced,8=>DamageType.Fire,
        9=>DamageType.Cold,10=>DamageType.Lightning,11=>DamageType.Poison,12=>DamageType.Disease,
        13=>DamageType.Divine,14=>DamageType.Magic,15=>DamageType.Sonic,16=>DamageType.Acid,
        17=>DamageType.Force,18=>DamageType.Death,19=>DamageType.Mind,20=>DamageType.Plant,
        21=>DamageType.Defensive,22=>DamageType.Demolition,23=>DamageType.SlowPoison,
        24=>DamageType.SpiritLink,25=>DamageType.ShadowStrike,26=>DamageType.Universal,
        _=>DamageType.Normal
    };
    public static int ToJassId(this DamageType type)=>type switch
    {
        DamageType.Unknown=>0,DamageType.Normal=>4,DamageType.Enhanced=>5,DamageType.Fire=>8,
        DamageType.Cold=>9,DamageType.Lightning=>10,DamageType.Poison=>11,DamageType.Disease=>12,
        DamageType.Divine=>13,DamageType.Magic=>14,DamageType.Sonic=>15,DamageType.Acid=>16,
        DamageType.Force=>17,DamageType.Death=>18,DamageType.Mind=>19,DamageType.Plant=>20,
        DamageType.Defensive=>21,DamageType.Demolition=>22,DamageType.SlowPoison=>23,
        DamageType.SpiritLink=>24,DamageType.ShadowStrike=>25,DamageType.Universal=>26,
        _=>4
    };
}
