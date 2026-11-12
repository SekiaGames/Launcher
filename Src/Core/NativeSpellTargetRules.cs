namespace OpenWC3;

// Shared Game.dll 654740 permission gate, independent from authored target masks
// and ability-specific predicates. Unknown ability families retain their own rules.
public static partial class NativeSpellTargetRules
{
    readonly record struct Profile(ulong MagicLow,ulong MagicHigh,ulong EtherealLow,ulong EtherealHigh);
    public static int Error(string code,bool magicImmune,bool ethereal,bool physicalImmune,
        bool reverseShared,bool resistUltimates,bool ultimate,bool ignoreMagic=false,int channelOptions=0,
        bool ensnareIsMagic=false,bool webIsMagic=false,bool drainTransferException=false)
    {
        if(Find(code) is not {} profile)return 0;
        if(code is "Aens" or "Aweb")
        {
            bool magic=code=="Aens"?ensnareIsMagic:webIsMagic;
            if(magic)return magicImmune&&!ignoreMagic&&(resistUltimates||!ultimate)?0x36:0;
            return ethereal?0xbc:0;
        }
        if(code=="AHdr"&&drainTransferException&&reverseShared)magicImmune=false;
        if(code=="ANcl"&&(channelOptions&8)!=0)ignoreMagic=true;
        int index=(magicImmune?32:0)|(ethereal?16:0)|(reverseShared?8:0)
            |(resistUltimates?4:0)|(ultimate?2:0)|(ignoreMagic?1:0);
        ulong bit=1UL<<index;
        if(((physicalImmune?profile.MagicHigh:profile.MagicLow)&bit)!=0)return 0x36;
        if(code=="ANcl"&&(channelOptions&4)!=0&&ethereal)return 0xbc;
        return ((physicalImmune?profile.EtherealHigh:profile.EtherealLow)&bit)!=0?0xbc:0;
    }

    public static bool Matches(Ability ability,Unit target,string fallbackCode=null)
    {
        if(target==null)return true;
        // Both native state helpers bypass dead units; corpse admission is
        // independently controlled by the mask and resurrection predicate.
        if(!target.Alive)return true;
        Unit caster=ability.Owner;
        if(caster==null)return false;
        string code=ability.ObjectData?.GetString("code",ability.ObjectData.BaseId)??ability.Rawcode;
        if(string.IsNullOrEmpty(code))code=fallbackCode;
        var config=caster.World?.GameplayConfig??War3GameplayConfig.Default;
        // The native helper's ECX is the recipient, and its argument is caster.
        bool reverseShared=caster.World?.SharedSpellsQuery(target,caster)??target.Team==caster.Team;
        return Error(code,target.MagicImmune,target.Ethereal,target.State.Is(StateModType.PhysicalImmune),
            reverseShared,config.MagicImmunesResistUltimates!=0,ability.RequiredHeroLevel(1)>1,
            ability.IgnoresNativeMagicTargetRules,ability.NativeChannelTargetOptions,
            config.EnsnareIsMagic,config.WebIsMagic,code=="AHdr"&&DrainShares(ability,config))==0;
    }

    static bool DrainShares(Ability ability,War3GameplayConfig config)
        =>config.DrainTransfersLife&&(ability.ObjectData?.GetFloat("DataD",Math.Max(1,ability.Level),0,0)??0)!=0
            ||config.DrainTransfersMana&&(ability.ObjectData?.GetFloat("DataE",Math.Max(1,ability.Level),0,0)??0)!=0;

    public static bool IsMagicalEffect(Ability ability)
    {
        if(ability==null)return true; // Native shield permits a missing ability reference.
        string code=ability.ObjectData?.GetString("code",ability.ObjectData.BaseId)??ability.Rawcode;
        var config=ability.Owner?.World?.GameplayConfig??War3GameplayConfig.Default;
        return code switch
        {
            "Aens"=>config.EnsnareIsMagic,
            "Aweb"=>config.WebIsMagic,
            "ANcl"=>(ability.NativeChannelTargetOptions&8)==0,
            _=>FindMagicalEffect(code,ability.RequiredHeroLevel(1)>1)??true,
        };
    }
}
