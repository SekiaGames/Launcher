namespace OpenWC3;

// 光环 Buff 基类 (对标 CBuffAuraBase): 持源引用, 离场 2s 衰减后自删
public abstract class AuraBuff : Buff
{
    public override bool TickRunsWhilePaused=>true;
    public override bool Aura => true;
    public Unit Source;            // 光环来源(离开范围则衰减)
    public AuraAbility SourceAura;
    public int AuraLevel;
    float _outOfRangeTimer;
    float _visualAge;
    const float DecayTime = 2f;    // 离场后保留 2s (对标 AURA_BUFF_DECAY_TIME)

    // 光环 buff 不走到期 tick(Duration/RemainingSeconds 恒为 0), 基类的
    // "Duration - RemainingSeconds" 会固定返回 0, 让物编 TargetArt 永远停在第
    // 0 帧。生命之泉(ANrl)/魔法之泉(ANrm)的回复特效正是靠 buff TargetArt 表现,
    // 因此这里按真实存活时间推进动画。
    public override float VisualAgeSeconds => _visualAge;

    public void Refresh() { _outOfRangeTimer = 0; }   // 续命(仍在范围内)

    // 光环 Buff 不走基类固定过期; 由源离场衰减驱动
    //
    // 2026-10-03 删除了 OnAdd 的「<单位> 进入 [<光环>] 光环」逐条日志 ——
    //   半人马弓箭手一类**大量单位**每帧进出光环会产生刷屏日志(用户实机报告),
    //   而这条信息对排查没有价值(光环是否生效看 buff 加成即可)。
    // 2026-10-04 用户要求一并删除「光环衰减结束」日志(同样按要求去掉, 不再保留)。
    //   原 Trace() 辅助方法随之移除 —— 它只服务这一条日志, 留着就是死代码,
    //   且注释会误导后人以为还有其它光环日志。

    public override void OnAdd() => OnBuffAdd();
    public override void OnRemove() => OnBuffRemove();
    public override void OnTick(float dt)
    {
        _visualAge+=MathF.Max(0,dt);
        // 源失效/离场 → 计时衰减; 仍在范围 → 持续刷新
        var aura = SourceAura;
        if (Source == null || !Source.Alive || aura == null
            || aura.IsRuntimeLocked
            || !aura.CanBestowInCurrentForm
            || !Source.Abilities.Items.Contains(aura)
            || !aura.CanRetainBuff(this)
            || Vec2.Dist(Owner.Pos, Source.Pos) > aura.Radius)
        {
            _outOfRangeTimer += dt;
            if (_outOfRangeTimer >= DecayTime)
            {
                Owner.RemoveAbility(this);
            }
        }
        else _outOfRangeTimer = 0;
    }
}
