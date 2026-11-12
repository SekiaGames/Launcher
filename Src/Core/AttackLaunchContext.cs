namespace OpenWC3;

// Native melee effect dispatch (6620E0) precedes the ordinary damage packet.
public interface IAttackPreDamageEffect
{
    void BeforeAttackDamage(Unit target,in DamageData damage);
}

/// <summary>Launch-time resource/ability decisions retained by one attack, including in-flight missiles.</summary>
public interface IAttackLaunchModifier
{
    // 671a50: 0 is a host-independent modifier; 0b..19 share the main
    // selection, 1a has its own selection. A payload need not consume a slot:
    // CriticalStrike/Bash's 4c54e0 returns zero even when it modifies a packet.
    int AttackEffectPriority=>0;
    bool StopsAttackEffectSelection=>true;
    IAttackImpactPayload PrepareAttack(Unit target);
    IAttackImpactPayload PrepareAttack(Unit target,Weapon weapon)=>PrepareAttack(target);
}
public interface IAttackImpactPayload
{
    bool CannotMiss=>false;
    void Apply(Unit target,ref DamageData damage);
    void BeforeAttackDamage(Unit target,in DamageData damage) { }
    void AfterAttackDamage(Unit target,float dealt,in DamageData damage) { }
    string MissileArtOverride=>null;
    AttackSplashProfile? SplashOverride=>null;
    void OnMissileImpact(World world,Vec2 point) { }
}
public readonly record struct AttackSplashProfile(float Radius,TargetFlags Targets);
public sealed class AttackLaunchContext
{
    static readonly AttackLaunchContext Empty=new(null);
    readonly List<IAttackImpactPayload> _payloads;
    AttackLaunchContext(List<IAttackImpactPayload> payloads)=>_payloads=payloads;
    public static AttackLaunchContext Capture(Unit source,Unit target,Weapon weapon=null)
    {
        List<IAttackImpactPayload> payloads=null;
        var abilities=source.Abilities.Snapshot();
        // Roar/Howl and other independent launch modifiers do not compete
        // for the primary missile/effect slot.
        foreach(var ability in abilities)
            if(ability is IAttackLaunchModifier {AttackEffectPriority:0} modifier
                &&modifier.PrepareAttack(target,weapon) is {} payload)
                (payloads??=new()).Add(payload);
        bool selected=false;
        for(int priority=0xb;priority<=0x1a;priority++)
        {
            if(selected&&priority!=0x1a)continue;
            foreach(var ability in abilities)
            {
                if(ability is not IAttackLaunchModifier modifier
                    ||modifier.AttackEffectPriority!=priority
                    ||!source.Abilities.Contains(ability))continue;
                if(modifier.PrepareAttack(target,weapon) is not {} payload)continue;
                (payloads??=new()).Add(payload);
                if(!modifier.StopsAttackEffectSelection)continue;
                selected=true;break;
            }
        }
        return payloads==null?Empty:new(payloads);
    }
    public bool CannotMiss
    {
        get
        {
            if(_payloads!=null)foreach(var payload in _payloads)if(payload.CannotMiss)return true;
            return false;
        }
    }
    public string MissileArtOverride
    {
        get
        {
            string art=null;
            if(_payloads!=null)
                foreach(var payload in _payloads)
                    if(!string.IsNullOrWhiteSpace(payload.MissileArtOverride))art=payload.MissileArtOverride;
            return art;
        }
    }
    public AttackSplashProfile? SplashOverride
    {
        get
        {
            AttackSplashProfile? result=null;
            if(_payloads!=null)
                foreach(var payload in _payloads)
                    if(payload.SplashOverride is {} splash)result=splash;
            return result;
        }
    }
    public void Apply(Unit target,ref DamageData damage)
    {
        if(_payloads==null)return;
        foreach(var payload in _payloads)payload.Apply(target,ref damage);
    }
    public void OnMissileImpact(World world,Vec2 point)
    {
        if(_payloads==null)return;
        foreach(var payload in _payloads)payload.OnMissileImpact(world,point);
    }
    public void BeforeAttackDamage(Unit target,in DamageData damage)
    {
        if(_payloads!=null)foreach(var payload in _payloads)payload.BeforeAttackDamage(target,in damage);
    }
    public void AfterAttackDamage(Unit target,float dealt,in DamageData damage)
    {
        if(_payloads!=null)foreach(var payload in _payloads)payload.AfterAttackDamage(target,dealt,in damage);
    }
}
