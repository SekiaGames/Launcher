namespace OpenWC3;

// 光环能力 (对标 CAbilityAuraBase): 每 2s 扫描范围内目标, 授予/刷新光环 Buff (高等级覆盖)
// 子类决定: 范围/目标阵营筛选/具体 Buff 类型
public abstract class AuraAbility : Ability
{
    public override bool IsPassive=>true;
    // Aura ability TargetArt is the source's persistent ring. Recipient art
    // comes independently from the aura's BuffID, as in stock Warcraft III.
    public override bool HasPersistentTargetArts => true;
    // 41c9b0/3d99a0 aura timer dispatch is independent of owner +54.
    public override bool TickRunsWhilePaused=>true;
    public float Radius = 300f;
    public float CheckInterval = 2f;     // 周期检查间隔 (对标 AURA_PERIODIC_CHECK_TIME)
    public bool AffectsAllies = true;    // 光环作用于盟友(虔诚/专注) 还是敌人(减速)
    float _timer;
    int _auraLevel = 1;
    // ★ 报告 W11: 原来的 `readonly List<Unit> _scanUnits` 已删除 ——
    //   它只用于"每 2s 把全图单位拷一份", 现在改走World 的空间查询 + 池化列表,
    //   每单位每光环常驻一个 List 本身就是浪费(光环数量 = 单位数 × 技能数)。

    public override string Name => "光环";
    protected abstract AuraBuff CreateBuff(Unit source, Unit target, int level);
    protected virtual bool IsBuffCurrent(AuraBuff buff)=>true;
    public virtual bool CanRetainBuff(AuraBuff buff)=>true;
    protected virtual AuraBuff FindExistingBuff(Unit target)
    {
        IReadOnlyList<Ability> abilities=target.Abilities.Items;
        for(int i=0;i<abilities.Count;i++)
            if(abilities[i] is AuraBuff buff&&ReferenceEquals(buff.SourceAura,this))return buff;
        return null;
    }
    protected virtual bool RefreshExistingBuff(AuraBuff buff,int level)
    {
        if(buff.AuraLevel!=level||!IsBuffCurrent(buff))return false;
        buff.Refresh();return true;
    }

    // 3D99A0 checks this before creating OR refreshing any recipient, including
    // self. 4709E0 queries effect +198, not silence or the displayed skin.
    internal bool CanBestowInCurrentForm
    {
        get
        {
            if(Owner?.World?.GameplayConfig?.PolymorphedUnitsBestowAuras!=false)return true;
            var abilities=Owner.Abilities.Items;
            for(int i=0;i<abilities.Count;i++)
                if(abilities[i].IsPolymorphEffect)return false;
            return true;
        }
    }

    // Authored target masks use the same directional alliance classifier as
    // spells. AffectsAllies supplies the fallback for an unconfigured aura.
    protected virtual bool CanAffect(Unit target)
    {
        if (target == null || !target.Alive) return false;
        World? world = Owner?.World;
        if (world == null) return false;
        TargetFlags rules=HasConfiguredTargets?ConfiguredTargets:
            AffectsAllies?TargetFlags.Self|TargetFlags.Player|TargetFlags.Ally:TargetFlags.Enemy;
        if(!target.CanBeTargetedBy(Owner,rules,useHeroSpellRules:true))return false;
        return CanAffectExotic(target, world);
    }

    // 特殊单位是否施加光环。键名取自反编译的运行时常量(DotA war3mapMisc.txt),
    // 由 War3GameplayConfig 解析; 缺省一律不施加, 与原版一致。
    // Polymorph admission is shared by OnTick so derived target filters cannot
    // bypass it. Other exotic-source rules below retain their existing scope.
    protected virtual bool CanAffectExotic(Unit target, World world)
    {
        War3GameplayConfig? config = world.GameplayConfig;
        if (config == null) return true;
        if (Owner.IsHero && Owner.MovementType == MovementType.Fly
            && !config.FlyingHeroesBestowAuras) return false;
        // 3D99A0: illusion/invisible source gates are skipped for self recipients.
        if (!ReferenceEquals(target, Owner))
        {
            if (Owner.IsIllusion && !config.IllusionsBestowAuras) return false;
            if (Owner.Invisible && !config.InvisibleUnitsBestowAuras) return false;
        }
        // Animated/Burrowed 门需要变形/潜伏单位状态, 引擎尚无对应机制, 暂缺。
        return true;
    }

    public override void OnTick(float dt)
    {
        base.OnTick(dt);
        // 只有"可学技能"才需要未学习即不生效。天赋光环、物品附带光环
        // (物品附带的光环等)不是 Learnable, Level 可能为 0,
        // 原版按 1 级生效 —— 若沿用 Level<1 直接 return, 这类光环永远不触发。
        if (IsRuntimeLocked || Learnable && Level < 1) return;
        _auraLevel = Math.Max(1, Level);
        _timer -= dt;
        if (_timer > 0) return;
        _timer = CheckInterval;
        if(!CanBestowInCurrentForm)return;
        // 扫描范围内目标, 授予/刷新 Buff
        //
        // ★ 报告 W11: 此前是 `_scanUnits.AddRange(Owner.World.Units)` ——
        //   把**全地图单位**拷进临时列表, 再逐个判距离。即"光环数 × 全图单位数"
        //   的距离计算, 而真正在光环半径内的通常只有个位数。
        //   改用世界已有的空间哈希(CollectNearbyUnits)直接取半径内候选,
        //   复杂度降到 O(光环数 × 半径内单位数)。
        //
        //   候选列表走 RentUnitList/ReturnUnitList 池(池在 World.Spatial,
        //   支持嵌套查询): 不再每 2s 为每个光环分配一个新List, 也不引入
        //   跨光环共享的可变列表(那会在"一个光环正在遍历、另一个光环要复用
        //   同一份候选"时互相清空)。CreateBuff/AddAbility 内部若再触发
        //   别的空间查询,池的嵌套语义正好覆盖。
        List<Unit> candidates=Owner.World.RentUnitListForAura();
        try
        {
            Owner.World.CollectNearbyUnits(Owner.Pos,Radius,candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                Unit u = candidates[i];
                if (!u.Alive) continue;
                // 先判距离再判阵营: 原顺序是 CanAffect -> 距离, 等于对全世界每个单位
                // 都做一次联盟查询, 而光环每 2s 扫一次、每个光环一条, 是 O(光环数 x
                // 全世界单位数) 的无谓开销。距离是纯数值比较, 放前面等价且更便宜。
                // (CollectNearbyUnits 不做距离过滤, 所以这里仍需判距离。)
                if (Vec2.Dist(u.Pos, Owner.Pos) > Radius) continue;
                if (!CanAffect(u)) continue;
                // ★ 报告 S3: 此前 `foreach(Ability ability in u.Abilities)` 会因
                //   IEnumerable 而**装箱**; 改用索引器 + Count。
                //   注意容器没有 this[] 索引器, 要走 .Items[i]。
                AuraBuff existing=FindExistingBuff(u);
                if (existing != null && RefreshExistingBuff(existing,_auraLevel))continue;
                if (existing != null) u.RemoveAbility(existing); // 等级改变时按新数值重挂，避免旧增量残留
                var buff = CreateBuff(Owner, u, _auraLevel);
                buff.SourceAura = this;
                buff.AuraLevel = _auraLevel;
                u.AddAbility(buff);
            }
        }
        finally
        {
            Owner.World.ReturnUnitListForAura(candidates);
        }
    }
}
