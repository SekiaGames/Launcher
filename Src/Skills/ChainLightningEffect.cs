namespace OpenWC3;

// 弹射效果 (对标 CEffect/CMissile + AUTargetAI 目标链): 每 JumpDelay 跳一次, 找最近未命中敌人
public sealed class ChainLightningEffect : Effect
{
    readonly Unit caster;
    Unit previous;
    Unit current;
    float dmg, decay, range, jumpTimer;
    readonly string missileArt;
    readonly float missileSpeed;
    readonly TargetFlags targets;
    readonly IReadOnlyList<string> lightningEffects,targetArts,targetAttachments;
    readonly Func<Unit,bool> targetStatePredicate;
    int jumpsLeft;
    bool needsNextTarget;
    readonly HashSet<Unit> hit = new();
    // Game.dll 4c4d00 -> 4bd990 registers next-bounce event d01d2 at .25s.
    const float JumpDelay = 0.25f;

    public ChainLightningEffect(Unit c,Unit first,float dmg,float decay,
        float range,int jumps,string missileArt,float missileSpeed,
        TargetFlags targets=ChainLightning.AllowedTargets,
        IReadOnlyList<string> lightningEffects=null,IReadOnlyList<string> targetArts=null,
        IReadOnlyList<string> targetAttachments=null,Func<Unit,bool> targetStatePredicate=null)
    {
        caster=c;previous=c;current=first;this.dmg=dmg;this.decay=decay;
        this.range=range;jumpsLeft=jumps;this.missileArt=missileArt;
        this.missileSpeed=missileSpeed;
        this.targets=targets;
        this.lightningEffects=lightningEffects??new[]{"CLPB","CLSB"};
        this.targetArts=targetArts;this.targetAttachments=targetAttachments;
        this.targetStatePredicate=targetStatePredicate;
    }
    public override Vec2 Pos => current?.Pos ?? previous?.Pos ?? caster.Pos;
    public override string VKind => "lightning";
    public Unit CurrentTarget => current;

    public override bool Step(World w, float dt)
    {
        jumpTimer -= dt;
        if (jumpTimer > 1e-6f) return true;
        if(needsNextTarget)
        {
            // 4c6220(d01d2) -> 4a8bc0 -> 4c53e0: query at the event,
            // using the previous unit's current position and current candidates.
            if(previous==null||previous.Removed)return false;
            current=FindNextTarget(w);needsNextTarget=false;
        }
        if (current == null || !current.Alive || jumpsLeft <= 0) return false;

        hit.Add(current);
        SpellLightningVisuals.Spawn(w,previous,current,lightningEffects,
            hit.Count==1?0:1,targetArts,targetAttachments);
        if(previous!=null&&!string.IsNullOrWhiteSpace(missileArt))
            w.AddEffect(new AbilityMissileEffect(previous,current,missileArt,
                missileSpeed,0,onImpact:null));
        current.TakeDamage(new DamageData { Source = caster, Amount = dmg, AttackType = AttackType.Spells, DamageType = DamageType.Magic });
        w.Log($"    ⚡[闪电链] 命中 {current.Name} ({dmg:0} 魔法), 剩余跳数 {jumpsLeft - 1}");

        previous=current;
        jumpsLeft--; dmg *= decay; jumpTimer = JumpDelay;
        current=null;needsNextTarget=true;
        return jumpsLeft > 0;
    }

    Unit FindNextTarget(World w)
    {
        // AOcl 的后续跳与首跳使用同一 targetsAllowed。只按阵营/距离会让
        // Aloc/Untargetable 脚本 dummy 和路线标记吃掉弹射，日志看似有跳数，
        // 玩家却看不到有效单位之间的闪电链。
        //
        // ★ 报告 B11 + W10: 此前是
        //   EnemiesInRange(...).Where(...).OrderBy(...).ThenBy(...).FirstOrDefault()
        //   —— ① 每次分配一个 List; ② Where/OrderBy/ThenBy 三个延迟枚举器 +
        //      闭包捕获 previous; ③ OrderBy 会**缓冲整个序列**再排序。
        //   改为池化列表 + 单次 for 手算"最近、同分取更小 SimulationId",
        //   语义等价(平局规则与 ThenBy 一致), 零分配。
        Unit best=null;
        float bestDistance=float.MaxValue;
        List<Unit> candidates=w.RentEnemiesInRange(caster,previous.Pos,range);
        try
        {
            for(int i=0;i<candidates.Count;i++)
            {
                Unit candidate=candidates[i];
                if(hit.Contains(candidate)||candidate.Untargetable
                    ||targetStatePredicate?.Invoke(candidate)==false
                    ||!candidate.CanBeTargetedBy(caster,targets,useHeroSpellRules:true))continue;
                float distance=Vec2.Dist(candidate.Pos,previous.Pos);
                // 严格小于才替换 ⇒ 距离相同时保留先遇到的, 而先遇到的按
                // CollectNearbyUnits 的稳定顺序即较小 SimulationId(与 ThenBy 同构)。
                if(distance>=bestDistance)continue;
                best=candidate;bestDistance=distance;
            }
        }
        finally{w.ReturnEnemiesInRange(candidates);}
        return best;
    }

}

// AOcl 每一跳都要在起点和目标间保留一小段可见闪电。MissileArt 仍按物编数据
// 生成模型；这条效果表达的是两单位之间的连接，不能用一个沿路径移动的点模型替代。
public sealed class ChainLightningBeamEffect : Effect
{
    readonly float LifeSpan;
    bool cancelled;
    float age;
    readonly Unit source,target;
    readonly Vec2 start,end;

    public string LightningId { get; }
    public bool FadeOut { get; }
    public Unit Source=>source;
    public Unit Target=>target;

    public Vec2 Start=>source?.Removed==false?source.Pos:start;
    public Vec2 End=>target?.Removed==false?target.Pos:end;
    public float StartHeight=>(source?.FlyHeight??0f)+(source?.ProjectileLaunchZ??80f);
    public float EndHeight=>(target?.FlyHeight??0f)+(target?.ProjectileImpactZ??60f);
    public override float VisualAgeSeconds=>age;
    public float NormalizedAge => cancelled||LifeSpan<=0?1f:Math.Clamp(age / LifeSpan, 0f, 1f);

    public ChainLightningBeamEffect(Unit source, Unit target,float lifeSpan=.20f,
        string lightningId="CLPB",bool fadeOut=true)
    {
        LifeSpan=Math.Max(0,lifeSpan);
        this.source=source;this.target=target;LightningId=lightningId;
        FadeOut=fadeOut;
        start=source?.Pos??default;end=target?.Pos??start;
    }

    public override Vec2 Pos=>(Start+End)*0.5f;
    public override string VKind=>"chain-lightning";
    public void Cancel()=>cancelled=true;
    public override bool Step(World w,float dt)
    {
        age+=Math.Max(0f,dt);
        return !cancelled&&age<LifeSpan;
    }
}

// LightningEffect lists use the first entry for the primary bolt and the second
// for bounces. A single entry applies to both; an explicit empty list hides beams.
public static class SpellLightningVisuals
{
    public static ChainLightningBeamEffect CreateBeam(World world,Unit source,Unit target,
        IReadOnlyList<string> lightningIds,int boltIndex=0,float lifeSpan=.20f,bool fadeOut=true)
    {
        if(world==null||source==null||target==null||lightningIds?.Count is not >0)return null;
        var beam=new ChainLightningBeamEffect(source,target,lifeSpan,
            lightningIds[Math.Clamp(boltIndex,0,lightningIds.Count-1)],fadeOut);
        world.AddEffect(beam);return beam;
    }
    public static void Spawn(World world,Unit source,Unit target,
        IReadOnlyList<string> lightningIds,int boltIndex,
        IReadOnlyList<string> targetArts=null,IReadOnlyList<string> attachments=null,
        float lifeSpan=.20f)
    {
        if(target==null)return;
        CreateBeam(world,source,target,lightningIds,boltIndex,lifeSpan);
        if(targetArts==null)return;
        foreach(string art in targetArts)
        {
            if(attachments?.Count>0)
                foreach(string attachment in attachments)
                    world.AddSpecialEffectTarget(art,target,attachment,lifeSpan:1f);
            else world.AddSpecialEffectTarget(art,target,"origin",lifeSpan:1f);
        }
    }
}
