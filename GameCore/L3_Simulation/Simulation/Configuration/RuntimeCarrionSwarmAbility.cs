namespace OpenWC3.Game;

/// <summary>
/// 恐惧魔王腐臭蜂群（AUcs / Carrion Swarm）的原版基类语义：
/// 飞弹沿施法方向移动，命中半径从 Area 线性扩张到 DataD；
/// 每个合法单位只命中一次，并受 DataB 最大总伤害约束。
/// </summary>
public class RuntimeCarrionSwarmAbility : Ability
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.UnitOrPoint;
    protected override bool SpawnSpecialArtsOnCast=>false;
    public float DamagePerTarget { get; private set; }
    public float MaximumTotalDamage { get; private set; }
    public float WaveDistance { get; private set; }
    public float FinalArea { get; private set; }
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshCarrionSwarmData()
    {
        int level=Math.Max(1,Level);
        DamagePerTarget=Math.Max(0,
            RuntimeAbilityFactory.ReadFloat(ObjectData,"DataA",level,0));
        MaximumTotalDamage=Math.Max(0,
            RuntimeAbilityFactory.ReadFloat(ObjectData,"DataB",level,0));
        WaveDistance=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataC",level,0);
        FinalArea=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataD",level,ConfiguredAreaOfEffect));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));

        if((_allowedTargets&TargetRules.Relations)==0)
            _allowedTargets|=TargetFlags.Enemy;
        if((_allowedTargets&TargetRules.Shapes)==0)
            _allowedTargets|=TargetFlags.Ground|TargetFlags.Air;
        if((_allowedTargets&(TargetFlags.Alive|TargetFlags.Dead))==0)
            _allowedTargets|=TargetFlags.Alive;
        if((_allowedTargets&(TargetFlags.Vulnerable
                |TargetFlags.Invulnerable))==0)
            _allowedTargets|=TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshCarrionSwarmData();
    public override void OnLevelDown()=>RefreshCarrionSwarmData();

    public override bool CheckCanTarget(Unit target,Vec2 point)=>
        target==null||target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);

    public override void OnEffect(Unit target,Vec2 point)
    {
        World world=Owner?.World;
        if(world==null||(!AppliesOnZeroDamage&&DamagePerTarget<=0)
            ||MathF.Abs(WaveDistance)<=.001f)
            return;

        Vec2 targetPoint=target?.Pos??point;
        Vec2 direction=targetPoint-Owner.Pos;
        if(direction.Length<=.001f)
            direction=new Vec2(MathF.Cos(Owner.Facing),MathF.Sin(Owner.Facing));
        else direction=direction.Normalized;
        if(WaveDistance<0)direction=direction*-1f;

        Unit source=Owner;
        TargetFlags allowedTargets=_allowedTargets;
        var damageFor=CaptureWaveDamage();
        bool sharedBudget=UsesSharedLifeDamageBudget;
        bool appliesOnZero=AppliesOnZeroDamage;
        DamageType damageType=WaveDamageType;
        Vec2 origin=source.Pos;
        float distance=MathF.Abs(WaveDistance);
        float initialArea=Math.Max(0,ConfiguredAreaOfEffect);
        float finalArea=FinalArea;
        float remainingDamage=MaximumTotalDamage>0
            ?MaximumTotalDamage:float.PositiveInfinity;
        var hitUnits=new HashSet<Unit>();
        float speed=MissileSpeed>0?MissileSpeed:1100f;

        // Native 508540: each 0.02s missile tick searches one circle at the
        // missile's current position (max 64 units collected per batch, see
        // 4f0b60) while the remaining distance exceeds 0.01. Production frames
        // are coarser, so search both sweep endpoints to approximate the
        // native sample grid; shared endpoints are searched only once.
        float searchedTo=-1f;
        world.AddEffect(new AbilityLineMissileEffect(source,direction,distance,
            MissileArt,speed,(sweepWorld,fromDistance,toDistance)=>
            {
                // ★ 地面变形钩子: 冲击波(AOsh) 沿推进路径压出一道沟槽,
                //   尸虫群(AImt) 没有这个表现, 所以默认空实现, 由子类覆写。
                //   放在扫掠回调里(而不是 OnEffect 一次性)才能跟着波前一起推进。
                OnWaveSweepGround(sweepWorld,origin,direction,
                    fromDistance,toDistance,distance);
                if(remainingDamage<=0)return;
                SearchCircle(sweepWorld,fromDistance);
                SearchCircle(sweepWorld,toDistance);
            }));
        void SearchCircle(World sweepWorld,float front)
        {
            if(front<=searchedTo+.001f)return;
            searchedTo=front;
            float radius=AreaAt(front,distance,initialArea,finalArea);
            Vec2 center=origin+direction*front;
            Unit[] candidates=RuntimeSpatialUnitQuery.Around(sweepWorld,center,radius)
                .Where(candidate=>candidate.Alive&&!candidate.Removed
                    &&!hitUnits.Contains(candidate)
                    &&candidate.CanBeTargetedBy(source,allowedTargets,useHeroSpellRules:true)
                    &&Vec2.Dist(candidate.Pos,center)<=radius)
                .OrderBy(candidate=>Projection(origin,direction,candidate.Pos))
                .ThenBy(candidate=>candidate.SimulationId)
                .Take(64)
                .ToArray();

            float perTargetBudget=sharedBudget&&candidates.Length>0
                ?remainingDamage/candidates.Length:remainingDamage;
            foreach(Unit candidate in candidates)
            {
                if(!hitUnits.Add(candidate))continue;
                float damage=Math.Min(damageFor(candidate),
                    sharedBudget?perTargetBudget:remainingDamage);
                if(damage<=0&&!appliesOnZero)break;
                if(!sharedBudget)remainingDamage-=damage;
                foreach(string art in SpecialArts)
                    SpawnAttachedArt(art,candidate,SpecialAttachments,
                        ObjectDataArtLifeSpan);
                float lifeBefore=candidate.Life;
                float dealt=damage<=0?0:candidate.TakeDamage(new DamageData
                {
                    Source=source,Amount=damage,
                    AttackType=AttackType.Spells,DamageType=damageType,
                });
                if(sharedBudget)
                    remainingDamage-=lifeBefore-candidate.Life;
                AfterUnitHit(sweepWorld,candidate,damage,dealt);
                sweepWorld.Log($"[{LogTag}] {Rawcode}/{Name} -> "
                    +$"{candidate.Name} configured={damage:0.##}, "
                    +$"dealt={dealt:0.##}");
                if(!sharedBudget&&remainingDamage<=0)break;
            }
        }
    }

    protected virtual string LogTag=>"carrion-swarm";
    protected virtual bool AppliesOnZeroDamage=>false;
    // CMissileCarrionSwarm +0C -> 500560 -> 508540 shares the remaining
    // budget across this search and debits actual life lost, including overkill.
    // Derived fire/frost missiles have separate native implementations.
    protected virtual bool UsesSharedLifeDamageBudget=>GetType()==typeof(RuntimeCarrionSwarmAbility);
    // 4EB170 constructs the native packet with damage mask 0x8000 (Sonic).
    protected virtual DamageType WaveDamageType=>GetType()==typeof(RuntimeCarrionSwarmAbility)
        ?DamageType.Sonic:DamageType.Magic;
    // Shared 4FAEE0 copies DataA into the missile; never reread a live
    // ability level when a previously released wave reaches its victim.
    protected virtual Func<Unit,float> CaptureWaveDamage()
    {
        float damage=DamagePerTarget;
        return _=>damage;
    }
    protected virtual void AfterUnitHit(World world,Unit target,
        float configuredDamage,float dealt) { }

    /// <summary>
    /// 随波前推进的地面变形钩子（每帧扫掠区间都会调用一次）。
    /// 基类为空实现 —— 尸虫群不该在地面留痕；只有冲击波(AOsh) 覆写。
    /// </summary>
    /// <param name="fromDistance">本帧波前推进到的起始距离（沿方向轴）。</param>
    /// <param name="toDistance">本帧波前推进到的结束距离。</param>
    /// <param name="totalDistance">整道波的总长度。</param>
    protected virtual void OnWaveSweepGround(World world,Vec2 origin,
        Vec2 direction,float fromDistance,float toDistance,float totalDistance)
    {
    }

    static float Projection(Vec2 origin,Vec2 direction,Vec2 point)
    {
        Vec2 offset=point-origin;
        return offset.X*direction.X+offset.Y*direction.Y;
    }

    static float AreaAt(float projection,float distance,
        float initialArea,float finalArea)
    {
        float progress=distance<=.001f?1f:
            Math.Clamp(projection/distance,0f,1f);
        return initialArea+(finalArea-initialArea)*progress;
    }
}
