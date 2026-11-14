namespace OpenWC3.Game;

/// <summary>CAbilityRainOfFire（ANrf）：多波范围伤害并刷新持续燃烧。</summary>
public sealed class RuntimeRainOfFireAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;
    Vec2 _targetPoint;
    RainEffect _effect;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Point;
    public override float ExpectedChannelDuration=>WaveCount*WaveInterval;
    public int WaveCount { get; private set; }
    public float DamagePerWave { get; private set; }
    public int ShardCount { get; private set; }
    public float WaveInterval { get; private set; }
    public float BuildingDamageFactor { get; private set; }
    public float BurnDamagePerSecond { get; private set; }
    public float MaximumDamagePerWave { get; private set; }
    public float BurnDuration { get; private set; }
    public string BurnBuffRawcode { get; private set; }="";
    public string EffectRawcode { get; private set; }="";
    public int CompletedWaves=>_effect?.Completed??0;

    public void RefreshRainOfFireData()
    {
        int level=Math.Max(1,Level);
        WaveCount=Math.Max(1,RuntimeAbilityFactory.ReadInt(
            ObjectData,"DataA",level,0));
        DamagePerWave=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0));
        ShardCount=Math.Clamp(RuntimeAbilityFactory.ReadInt(
            ObjectData,"DataC",level,0),1,32);
        WaveInterval=Math.Max(World.TickSeconds,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Cast",level,0));
        BuildingDamageFactor=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataD",level,0);
        BurnDamagePerSecond=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataE",level,0));
        MaximumDamagePerWave=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataF",level,float.PositiveInfinity));
        BurnDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        var buffs=RuntimeAbilityFactory.ReadString(ObjectData,"BuffID",level)
            .SplitConfig(',',StringSplitOptions.RemoveEmptyEntries
                |StringSplitOptions.TrimEntries);
        BurnBuffRawcode=buffs.FirstOrDefault()??"";
        if(BurnBuffRawcode.Length>4)BurnBuffRawcode=BurnBuffRawcode[..4];
        EffectRawcode=RuntimeAbilityFactory.ReadEffectRawcode(ObjectData,level);
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if((_allowedTargets&TargetRules.Relations)==0)
            _allowedTargets|=TargetFlags.Enemy;
        if((_allowedTargets&TargetRules.Shapes)==0)
            // Stock ANrf omits a target shape; its area damage also hits buildings.
            _allowedTargets|=TargetFlags.Ground|TargetFlags.Air|TargetFlags.Structure;
        _allowedTargets|=TargetFlags.Alive|TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshRainOfFireData();
    public override void OnLevelDown()=>RefreshRainOfFireData();
    public override void OnDisabledStateChanged()
    {if((IsDisabled||IsRuntimeLocked)&&IsCasting)CancelCast();}
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null;

    public override void OnEffect(Unit target,Vec2 point)
    {
        if(Owner?.World==null)return;
        _effect?.StopLaunching();
        _targetPoint=point;
        _effect=new RainEffect(Owner.World,new WavePayload(this,objects,point),WaveCount,WaveInterval);
        Owner.World.AddEffect(_effect);
        Owner.Flags|=UnitFlags.Channeling;
    }

    public override void OnTick(float dt)
    {
        base.OnTick(dt);
        if(!IsChanneling)return;
        if(Owner.Silenced||IsDisabled||IsRuntimeLocked){FinishChannel(true);return;}
        if(_effect?.Done==true)FinishChannel(interrupted:false);
    }

    protected override void OnCastFinished(bool interrupted,bool successful)
    {
        // 49b220 subtracts future launches from remaining damage events;
        // already launched waves retain their delayed impacts.
        _effect?.StopLaunching();
    }

    void ApplyWave()=>new WavePayload(this,objects,_targetPoint).Impact(Owner.World);

    sealed class RainEffect : Effect
    {
        readonly WavePayload _payload;
        readonly double _start,_interval;
        readonly int _count;
        int _launched=1;
        bool _stopped;
        public int Completed { get; private set; }
        public bool Done=>(_stopped||_launched>=_count)&&Completed>=_launched;
        public override Vec2 Pos=>_payload.Point;
        public override string VKind=>"rain-of-fire";
        public RainEffect(World world,WavePayload payload,int count,float interval)
        {
            _payload=payload;_count=count;_interval=interval;
            _start=world.Tick*(double)World.TickSeconds;
            _payload.Launch(world);
        }
        public void StopLaunching()=>_stopped=true;
        public override bool Step(World world,float dt)
        {
            double age=world.Tick*(double)World.TickSeconds-_start;
            while(true)
            {
                double launch=!_stopped&&_launched<_count?_launched*_interval:double.PositiveInfinity;
                double impact=Completed<_launched?.9+Completed*_interval:double.PositiveInfinity;
                if(Math.Min(launch,impact)>age+.000001)break;
                if(launch<=impact){_launched++;_payload.Launch(world);}
                else{Completed++;_payload.Impact(world);}
            }
            return !Done;
        }
    }

    sealed class WavePayload
    {
        readonly RuntimeRainOfFireAbility _ability;
        readonly War3ObjectData _objects;
        readonly Unit _source;
        readonly float _radius,_damage,_maximum,_building,_burn,_duration;
        readonly int _shards,_level;
        readonly TargetFlags _targets;
        readonly string _art,_buff;
        public Vec2 Point { get; }
        public WavePayload(RuntimeRainOfFireAbility ability,War3ObjectData objects,Vec2 point)
        {
            _ability=ability;_objects=objects;_source=ability.Owner;Point=point;
            _radius=Math.Max(0,ability.ConfiguredAreaOfEffect);_damage=ability.DamagePerWave;
            _maximum=ability.MaximumDamagePerWave;_building=ability.BuildingDamageFactor;
            _burn=ability.BurnDamagePerSecond;_duration=ability.BurnDuration;
            _shards=ability.ShardCount;_targets=ability._allowedTargets;
            _art=ability.ResolveEffectArt();_buff=ability.BurnBuffRawcode;_level=Math.Max(1,ability.Level);
        }
        public void Impact(World world)
        {
            using var query=RuntimeSpatialUnitQuery.RentAround(world,Point,_radius);
            var targets=query.Units;int count=0;
            for(int i=0;i<targets.Count;i++)
            {
                Unit unit=targets[i];
                if(unit.Alive&&!unit.Removed&&Vec2.Dist(Point,unit.Pos)<=_radius
                    &&unit.CanBeTargetedBy(_source,_targets,useHeroSpellRules:true))targets[count++]=unit;
            }
            targets.RemoveRange(count,targets.Count-count);world.SortUnitsBySimulationId(targets);
            float damage=targets.Count>0&&_maximum>0&&_maximum<float.PositiveInfinity
                ?Math.Min(_damage,_maximum/targets.Count):_damage;
            foreach(Unit unit in targets)
            {
                if(damage>0)unit.TakeDamage(new DamageData
                {
                    Source=_source,Amount=damage*(unit.IsBuilding?_building:1),
                    AttackType=AttackType.Spells,DamageType=DamageType.Fire,
                });
                ApplyBurn(unit);
            }
        }

        void ApplyBurn(Unit unit)
        {
            if(_burn<=0||_duration<=0||!unit.Alive)return;
            foreach(RuntimeRainOfFireBuff old in unit.Abilities
                        .OfType<RuntimeRainOfFireBuff>()
                        .Where(buff=>ReferenceEquals(buff.Caster,_source)).ToArray())
                unit.RemoveAbility(old);
            var buff=new RuntimeRainOfFireBuff(_burn);
            BuffTypeData data=_objects?.GetBuff(_buff);
            if(data!=null)buff.BindBuffObjectData(
                _buff,data,_level,_source,_ability);
            else buff.BindObjectData(
                _buff,_ability.ObjectData,_level);
            buff.Duration=_duration;
            unit.AddAbility(buff);
        }

        public void Launch(World world)
        {
            if(string.IsNullOrWhiteSpace(_art))return;
            for(int index=0;index<_shards;index++)
            {
                float angle=world.Rng.NextSingle()*MathF.PI*2;
                float distance=world.Rng.NextSingle()*_radius;
                world.AddSpecialEffect(_art,Point
                    +new Vec2(MathF.Cos(angle),MathF.Sin(angle))*distance,
                    lifeSpan:2);
            }
        }
    }

    string ResolveEffectArt()
        => RuntimeAbilityFactory.ResolveEffectArt(EffectRawcode);
}

public sealed class RuntimeRainOfFireBuff(float damagePerSecond) : Buff
{
    public override bool Positive=>false;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Rain of Fire")??"Rain of Fire";
    public float DamagePerSecond=>damagePerSecond;
    protected override void OnBuffAdd() { }
    protected override void OnBuffRemove() { }
    public override void OnTick(float dt)
    {
        float active=Math.Min(Math.Max(0,dt),RemainingSeconds);
        if(active>0&&Owner.Alive)
            Owner.TakeDamage(new DamageData
            {
                Source=Caster,Amount=damagePerSecond*active,
                AttackType=AttackType.Spells,DamageType=DamageType.Magic,
            });
        base.OnTick(dt);
    }
}
