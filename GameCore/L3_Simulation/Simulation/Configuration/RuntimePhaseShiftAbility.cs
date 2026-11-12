namespace OpenWC3.Game;

/// <summary>Apsh: self phase, with damage-triggered autocast.</summary>
public sealed class RuntimePhaseShiftAbility(War3ObjectData objects) : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;
    public override bool SupportsAutoCast=>true;
    public float PhaseDuration=>Math.Max(0,GetLevelFloat("Dur"));
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null
        &&Owner?.Alive==true&&!Owner.Removed&&!Owner.State.Channeling
        &&!Owner.State.Is(StateModType.Phased)&&PhaseDuration>0;
    public override void OnDamaged(ref DamageData damage,float dealt)
    {
        // Game.dll 6043f0 -> 66e630: source must resolve and be hostile.
        if(dealt<=0||!AutoCastActive||Owner?.Life<=0||Owner?.Alive!=true
            ||damage.Source==null||Owner.World?.AreEnemies(Owner,damage.Source)!=true
            ||Owner.Hidden||Owner.State.Paused||Owner.State.Sleeping
            ||Owner.State.Is(StateModType.DisableAutoCast)||!CheckCanUse(null,Owner.Pos))return;
        Begin(null,Owner.Pos);
    }
    public override void OnEffect(Unit target,Vec2 point)
    {
        if(!CheckTarget(target,point))return;
        string rawcode=BuffRawcodes.FirstOrDefault()??"Bpsh";
        var buff=new RuntimePhaseShiftBuff{Caster=Owner,SourceAbility=this};
        var data=objects?.GetBuff(rawcode);
        if(data!=null)buff.BindBuffObjectData(rawcode,data,Math.Max(1,Level),Owner,this);
        else buff.BindObjectData(rawcode,ObjectData,Math.Max(1,Level));
        buff.Duration=PhaseDuration;
        Owner.AddAbility(buff);
    }
    public override void OnRemove()
    {
        if(Owner==null)return;
        foreach(var buff in Owner.Abilities.OfType<RuntimePhaseShiftBuff>()
            .Where(buff=>ReferenceEquals(buff.SourceAbility,this)).ToArray())Owner.RemoveAbility(buff);
    }
}

public sealed class RuntimePhaseShiftBuff : Buff
{
    public override string Name=>"Phase Shift";
    public override bool AutoDispel=>false;
    public override bool Magic=>false;
    bool _applied;
    protected override void OnBuffAdd()
    {
        Owner.AddState(StateModType.Phased);_applied=true;
    }
    protected override void OnBuffRemove()
    {
        if(!_applied)return;
        Owner.RemoveState(StateModType.Phased);_applied=false;
    }
}
