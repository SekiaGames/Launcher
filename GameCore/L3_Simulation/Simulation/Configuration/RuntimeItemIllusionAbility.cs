namespace OpenWC3.Game;

/// <summary>Item Illusion（AIil）：复制目标单位，应用出伤/承伤倍率并赋予限时生命。</summary>
public sealed class RuntimeItemIllusionAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Unit;
    public float DamageDealtFactor { get; private set; }
    public float DamageReceivedFactor { get; private set; }
    public float NormalDuration { get; private set; }
    public float HeroDuration { get; private set; }
    public string IllusionBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;
    public Unit LastCreatedIllusion { get; private set; }

    public void RefreshItemIllusionData()
    {
        int level=Math.Max(1,Level);
        DamageDealtFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,1));
        DamageReceivedFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,1));
        NormalDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        HeroDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"HeroDur",level,NormalDuration));
        IllusionBuffRawcode=RuntimeIllusionModifierBuff.ReadBuffRawcode(ObjectData,level);
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Player|TargetFlags.Ally|TargetFlags.Self|TargetFlags.Alive
                |TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshItemIllusionData();
    public override void OnLevelDown()=>RefreshItemIllusionData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>
        target?.Alive==true&&!target.IsBuilding
        &&target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);

    public override void OnEffect(Unit target,Vec2 point)
    {
        World world=Owner?.World;
        if(world==null||target?.Alive!=true||string.IsNullOrWhiteSpace(target.TypeId))
            return;
        int heroLevel=target.Abilities.Get<Hero>()?.HeroLevel??0;
        Unit illusion=UnitFactory.Create(objects,target.TypeId,Owner.Team,
            target.Pos+new Vec2(MathF.Cos(target.Facing),MathF.Sin(target.Facing))
                *MathF.Max(48,target.CollisionSize*2+8),target.Facing,heroLevel:heroLevel);
        illusion.PlayerId=Owner.PlayerId;
        illusion.TeamColorIndex=Owner.TeamColorIndex;
        illusion.IsIllusion=true;
        UnitFactory.CopyIllusionHeroAttributes(target,illusion);
        illusion.Classifications=(illusion.Classifications&~Classification.Summoned)
            |(target.Classifications&Classification.Summoned);
        illusion.VertexColorRgba=target.VertexColorRgba;
        illusion.VisualScale=target.VisualScale;
        illusion.VisualScaleX=target.VisualScaleX;
        illusion.VisualScaleY=target.VisualScaleY;
        illusion.VisualScaleZ=target.VisualScaleZ;
        world.SpawnViaCreateUnit(illusion);
        UnitFactory.CompleteIllusionCopy(target,illusion);

        float duration=target.IsHero?HeroDuration:NormalDuration;
        if(duration>0)
            RuntimeAbilityFactory.ApplyTimedLife(objects,illusion,"BTLF",duration);
        var modifier=new RuntimeIllusionModifierBuff(
            DamageDealtFactor,DamageReceivedFactor);
        BuffTypeData data=objects?.GetBuff(IllusionBuffRawcode);
        if(data!=null)modifier.BindBuffObjectData(IllusionBuffRawcode,data,
            Math.Max(1,Level),Owner,this);
        else modifier.BindObjectData(IllusionBuffRawcode,ObjectData,
            Math.Max(1,Level));
        // Buff 使用 int 绝对 tick，不能用 float.MaxValue（转换会溢出）。
        modifier.Duration=duration>0?duration:1_000_000f;
        illusion.AddAbility(modifier);
        LastCreatedIllusion=illusion;
        world.Events.Fire(new EventContext
        {
            Event=GameEvent.UnitSummoned,
            Source=Owner,
            Target=illusion,
            TriggerUnit=Owner,
            TriggerPlayerId=Owner.PlayerId,
            SummoningUnit=Owner,
            SummonedUnit=illusion,
        });
    }
}

public class RuntimeIllusionModifierBuff(
    float damageDealtFactor,float damageReceivedFactor) : Buff
{
    bool _disappearancePlayed;
    internal static string ReadBuffRawcode(IRuntimeObjectData data,int level)
    {
        // Extended custom abilities can have more levels than the inherited
        // BuffID list. Preserve its last authored entry instead of losing the
        // buff identity (and death art) at those higher levels.
        for(int configured=Math.Max(1,level);configured>=1;configured--)
        {
            string value=data?.GetString("BuffID",configured,0,null);
            if(value==null)continue;
            value=value.Trim();
            if(value is "" or "_" or "-")return "";
            return value.Length>4?value[..4]:value;
        }
        return "";
    }
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Illusion")??"Illusion";
    public float DamageDealtFactor=>damageDealtFactor;
    public float DamageReceivedFactor=>damageReceivedFactor;
    // Native timed-life traits: ordinary dispels cannot strip damage factors
    // from an image that survives their separate summon-damage packet.
    public override bool Magic=>false;
    public override bool AutoDispel=>false;
    public override bool TimedLife=>true;
    public override bool SpellStealable=>false;

    protected override void OnBuffAdd() { }
    protected override void OnBuffRemove() { }
    // The unit's timed-life clock owns expiry. A second Buff clock can strip
    // this modifier one tick before the image actually dies.
    public override void OnTick(float dt) { }
    public override void OnDeath()
    {
        if(!_disappearancePlayed&&Owner?.IsIllusion==true&&!Owner.Removed)
        {
            _disappearancePlayed=true;
            // Game.dll 514df0 -> 417e80 selects only the first Specialart
            // from the illusion Buff and creates it at the departing unit.
            // It must remain a world effect after the unit has been retired.
            string raw=ObjectData is BuffTypeData buffData
                ?buffData.GetString("Specialart",Math.Max(1,Level),0,""):"";
            string art=RuntimeConfigCache.ReadArts(raw).FirstOrDefault();
            if(!string.IsNullOrWhiteSpace(art))
                Owner.World?.AddSpecialEffect(art,Owner.Pos,lifeSpan:ObjectDataArtLifeSpan,height:Owner.FlyHeight);
        }
        base.OnDeath();
    }
    public override void ModifyOutgoingDamage(ref DamageData d)=>
        d.DamageMultiplier*=damageDealtFactor;
    public override int IncomingDamagePriority=>3; // CBuffItemIllusion +f8 62d910.
    public override void ModifyIncomingDamage(ref DamageData d)=>
        d.DamageMultiplier*=damageReceivedFactor;
}
