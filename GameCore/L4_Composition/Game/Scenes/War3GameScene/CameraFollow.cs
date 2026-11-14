using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 镜头跟随。对应客户端 <c>War3CameraComponent.Update</c> 里那四条每帧驱动：
/// 肖像跟随、锁定跟随、总跟随（跟随选中队列）、跟随标记单位。
///
/// 共同点（客户端的口径，这里逐条对齐）：
///   · 喂给镜头的是**渲染插值坐标**而不是 20Hz 仿真坐标 —— 仿真坐标直接喂镜头，
///     60FPS 下就是"一格一格跳"。样本由 <see cref="AdvanceFollowSample"/> 按单位速度
///     每渲染帧追赶仿真坐标（与客户端 RenderPosition 同一套）。
///   · 优先级最低：电影模型镜头 / 脚本目标控制器 / 脚本平移挡在前面时，本帧不喂。
///   · 玩家拖镜头（中键）打开 0.1 秒"失锁定窗口"，窗口内解绑让位，计时归零后自动滑回。
///   · 肖像跟随松手后**不立刻停**：要等镜头真的滑到单位附近才解绑（客户端 32 单位）。
/// </summary>
public sealed partial class War3GameScene
{
    // ---- 肖像跟随（长按头像）----
    Unit _portraitFollowTarget;
    bool _portraitReleasePending;
    bool _wasPortraitFollowing;

    // ---- 锁定跟随（开启瞬间绑定当前聚焦单位，之后不随选择变化）----
    Unit _lockFollowUnit;
    bool _lockFollowActive;

    // ---- 总跟随（始终跟着当前聚焦的单位，带过滤）----
    bool _trackFollowActive;
    Unit _trackFollowTarget;
    readonly List<Unit> _trackStack = new();
    readonly List<Unit> _trackScratch = new();
    // 面板参数（客户端默认值：建筑参与，敌人/中立/友军都不参与）
    bool _trackAllowBuildings = true;
    bool _trackAllowEnemy;
    bool _trackAllowNeutral;
    bool _trackAllowAlly;
    bool _trackAutoSwitchToEarlier;
    bool _trackHeroFirstInQueue;
    bool _trackHeroTopInStack;

    // ---- 跟随标记单位 ----
    Unit _markedFollowUnit;
    bool _markedFollowActive;

    // ---- 渲染率样本（每个被跟随单位一份；换目标即重置）----
    Unit _followSampleUnit;
    Vector3 _followSamplePosition;

    // ---- 玩家拖镜头时的失锁定窗口 ----
    const float CameraPanUnlockSeconds = 0.1f;
    float _cameraPanUnlockRemaining;

    public bool HasSoftFollowController => _camera?.HasSoftFollowController == true;
    public bool CameraFollowTracking => _trackFollowActive;
    public bool CameraFollowLocked => _lockFollowActive;
    public bool MarkedCameraFollowing => _markedFollowActive;

    bool AnyCameraFollowActive =>
        _portraitFollowTarget != null || _lockFollowActive || _trackFollowActive || _markedFollowActive;

    bool ShouldReleaseCameraForPlayerPan =>
        _cameraPanUnlockRemaining > 0f && AnyCameraFollowActive;

    /// <summary>玩家拖动镜头（中键）时把失锁定计时重置到 0.1 秒。</summary>
    public void ResetCameraPanUnlockTimer()
    {
        if (AnyCameraFollowActive) _cameraPanUnlockRemaining = CameraPanUnlockSeconds;
    }

    void TickCameraPanUnlockTimer(float deltaTime)
    {
        if (_cameraPanUnlockRemaining <= 0f) return;
        if (Paused) return;
        if (ShouldReleaseCameraForPlayerPan) _camera?.ClearSoftFollowController();
        _cameraPanUnlockRemaining = MathF.Max(0f, _cameraPanUnlockRemaining - deltaTime);
    }

    /// <summary>
    /// 每帧驱动四条跟随。顺序与客户端一致（肖像 → 锁定 → 总跟随 → 标记），
    /// 由 <see cref="War3CameraComponent.Update"/> 在 <c>camera.Update</c> 之前调用。
    /// </summary>
    void UpdateCameraFollow(float deltaTime)
    {
        TickCameraPanUnlockTimer(deltaTime);
        UpdatePortraitFollow(deltaTime);
        UpdateLockFollow(deltaTime);
        UpdateTrackFollow(deltaTime);
        UpdateMarkedFollow(deltaTime);
        TickCameraFollowProbe(deltaTime);
    }

    // ------------------------------------------------------------------
    //  探针：--camera-follow=track|lock|marked|portrait
    //  选一个自己的单位、下一条移动令，然后 10Hz 打镜头目标点，
    //  用来看「跟随是否连续」以及「肖像跟随松手后什么时候解绑」。
    // ------------------------------------------------------------------

    float _followProbeRemaining;
    float _followProbeTimer;
    float _portraitProbeReleaseAt;
    Unit _followProbeUnit;
    string _followProbeMode;

    public void RunCameraFollowProbe(string mode)
    {
        _followProbeMode = mode;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer || unit.IsBuilding) continue;
            _followProbeUnit = unit;
            break;
        }
        if (_followProbeUnit == null)
        {
            GD.Print($"[FOLLOW] 探针 {mode}：没有可用单位");
            return;
        }

        ClearSelection();
        _selected.Add(_followProbeUnit);
        _followProbeUnit.View?.SetSelected(true);
        RefreshSelection();
        UpdateCommandCard();

        switch (mode)
        {
            case "lock": ToggleCameraFollowLock(); break;
            case "marked": ToggleCameraFollowMarked(); break;
            case "portrait": BeginPortraitFollow(_followProbeUnit); _portraitProbeReleaseAt = 1.5f; break;
            default: ToggleCameraFollowTrack(); break;
        }

        // 让它走起来，跟随的连续性才有东西可看。
        // 肖像跟随要验证「松手后等镜头贴合才解绑」，所以走一小段就停，让镜头追得上。
        Vector3 offset = mode == "portrait" ? new Vector3(260f, 0f, 90f) : new Vector3(700f, 0f, 260f);
        Vector3 goal = _followProbeUnit.Position + offset;
        IssueMoveOrders(goal, false);
        _followProbeRemaining = 5f;
        _followProbeTimer = 0f;
        GD.Print($"[FOLLOW] 探针 {mode}：单位 {_followProbeUnit.Type?.Id} 目标 {goal}");
    }

    void TickCameraFollowProbe(float deltaTime)
    {
        if (_followProbeRemaining <= 0f) return;
        _followProbeRemaining -= deltaTime;
        if (_portraitProbeReleaseAt > 0f)
        {
            _portraitProbeReleaseAt -= deltaTime;
            if (_portraitProbeReleaseAt <= 0f)
            {
                EndPortraitFollow();
                GD.Print("[FOLLOW] 探针 portrait：松手，等镜头贴合后解绑");
            }
        }
        _followProbeTimer -= deltaTime;
        if (_followProbeTimer > 0f) return;
        _followProbeTimer = 0.1f;
        Unit unit = _followProbeUnit;
        Vector3 cameraTarget = _camera?.Target ?? default;
        Vector3 viewTarget = _camera?.ViewTarget ?? default;
        GD.Print($"[FOLLOW] t={5f - _followProbeRemaining:0.0} "
            + $"unit=({unit?.Position.X:0.0},{unit?.Position.Z:0.0}) "
            + $"target=({cameraTarget.X:0.0},{cameraTarget.Z:0.0}) "
            + $"view=({viewTarget.X:0.0},{viewTarget.Z:0.0}) "
            + $"following={(_portraitFollowTarget != null || _lockFollowActive || _trackFollowActive || _markedFollowActive)}");
        if (_followProbeRemaining <= 0f)
            GD.Print($"[FOLLOW] 探针 {_followProbeMode} 结束");
    }

    /// <summary>镜头是否已被更高优先级的导演接管（跟随一律让位）。</summary>
    bool CameraFollowYielded =>
        Paused || _camera == null || _camera.HasCinematicModel;

    // ------------------------------------------------------------------
    //  肖像跟随：长按头像期间把镜头带到这个单位上
    // ------------------------------------------------------------------

    public void BeginPortraitFollow(Unit unit)
    {
        if (unit == null || _camera == null) return;
        _portraitFollowTarget = unit;
        _portraitReleasePending = false;
        _followSampleUnit = null;
    }

    /// <summary>
    /// 松手。客户端不在这里解绑 —— 先把 <see cref="_portraitReleasePending"/> 立起来，
    /// 等镜头滑到单位附近（32 单位）才真正松手，否则手指一抬镜头就"卡"在半路。
    /// </summary>
    public void EndPortraitFollow()
    {
        if (_portraitFollowTarget == null) return;
        _portraitReleasePending = true;
    }

    void UpdatePortraitFollow(float deltaTime)
    {
        Unit target = _portraitFollowTarget;
        bool following = target != null && target.Alive && !CameraFollowYielded;
        if (following)
        {
            Vector3 sample = AdvanceFollowSample(target, deltaTime);
            _camera.SetPresentationTargetController(target, sample);
            // 可见镜头已经贴到被渲染的单位身上 ⇒ 松手请求生效。
            float dx = _camera.ViewTarget.X - sample.X;
            float dz = _camera.ViewTarget.Z - sample.Z;
            if (dx * dx + dz * dz < 32f * 32f && _portraitReleasePending)
            {
                _portraitFollowTarget = null;
                _portraitReleasePending = false;
                _camera.ClearTargetController();
                following = false;
            }
        }
        else if (target == null || !target.Alive || CameraFollowYielded)
        {
            if (target != null && !target.Alive)
            {
                _portraitFollowTarget = null;
                _portraitReleasePending = false;
            }
        }
        if (!following && _wasPortraitFollowing && _portraitFollowTarget == null)
            _camera.ClearTargetController();
        _wasPortraitFollowing = following;
    }

    // ------------------------------------------------------------------
    //  锁定跟随：开启瞬间绑定当前聚焦单位，之后只跟这一个
    // ------------------------------------------------------------------

    /// <summary>开启/关闭锁定跟随。开启时绑定当前聚焦单位；没选中则沿用上次锁过的单位。</summary>
    public void ToggleCameraFollowLock()
    {
        if (_lockFollowActive)
        {
            _lockFollowActive = false;
            _camera?.ClearSoftFollowController();
            _log?.Add("镜头锁定跟随：关");
            return;
        }
        Unit focused = PrimarySelection;
        if (focused != null && focused.Alive) _lockFollowUnit = focused;
        if (_lockFollowUnit == null || !_lockFollowUnit.Alive)
        {
            _log?.Add("镜头锁定跟随：没有可锁定的单位");
            return;
        }
        _trackFollowActive = false;
        _markedFollowActive = false;
        _lockFollowActive = true;
        _followSampleUnit = null;
        _log?.Add($"镜头锁定跟随：{_lockFollowUnit.Type?.Name ?? "单位"}");
    }

    void UpdateLockFollow(float deltaTime)
    {
        if (!_lockFollowActive) return;
        Unit target = _lockFollowUnit;
        if (target == null)
        {
            _lockFollowActive = false;
            return;
        }
        if (!target.Alive)
        {
            _lockFollowActive = false;
            _camera?.ClearSoftFollowController();
            _log?.Add("曾锁定的单位已不在地图之中");
            return;
        }
        if (CameraFollowYielded || ShouldReleaseCameraForPlayerPan)
        {
            _camera?.ClearSoftFollowController();
            return;
        }
        _camera.SetSoftFollowController(target, AdvanceFollowSample(target, deltaTime));
    }

    // ------------------------------------------------------------------
    //  总跟随：始终跟着当前聚焦的单位（带过滤与回退）
    // ------------------------------------------------------------------

    public void ToggleCameraFollowTrack()
    {
        if (_trackFollowActive)
        {
            _trackFollowActive = false;
            _trackFollowTarget = null;
            _trackStack.Clear();
            _camera?.ClearSoftFollowController();
            _log?.Add("镜头总跟随：关");
            return;
        }
        _lockFollowActive = false;
        _markedFollowActive = false;
        _trackFollowActive = true;
        _trackFollowTarget = null;
        _followSampleUnit = null;
        _log?.Add("镜头总跟随：开（跟随当前选中的单位）");
    }

    /// <summary>中立玩家槽位判据（与客户端一致：12..15 是中立，0..11 才是真玩家）。</summary>
    static bool IsNeutralSlot(int player) => player is >= 12 and <= 15;

    void UpdateTrackFollow(float deltaTime)
    {
        if (!_trackFollowActive) return;
        if (CameraFollowYielded || ShouldReleaseCameraForPlayerPan)
        {
            _camera?.ClearSoftFollowController();
            return;
        }
        if (!_trackAutoSwitchToEarlier) _trackStack.Clear();

        Unit focus = ResolveTrackFocus();
        if (focus == null)
        {
            // 没有可聚焦对象 ⇒ 本帧不接管，但保留开启态（下次选中自动接上）。
            _trackFollowTarget = null;
            _camera?.ClearSoftFollowController();
            return;
        }
        _trackFollowTarget = focus;
        _camera.SetSoftFollowController(focus, AdvanceFollowSample(focus, deltaTime));
    }

    Unit ResolveTrackFocus()
    {
        List<Unit> candidates = _trackScratch;
        candidates.Clear();
        IReadOnlyList<Unit> items = _selection?.Items ?? _selected;
        for (int i = 0; i < items.Count; i++)
        {
            Unit unit = items[i];
            if (IsTrackable(unit)) candidates.Add(unit);
        }

        if (candidates.Count > 0)
        {
            Unit focus = null;
            if (_trackHeroFirstInQueue)
            {
                for (int i = candidates.Count - 1; i >= 0; i--)
                {
                    if (candidates[i]?.IsHero == true) { focus = candidates[i]; break; }
                }
            }
            focus ??= PickPrimaryOrFirst(candidates);
            if (_trackAutoSwitchToEarlier) PushTrackStack(focus);
            return focus;
        }

        Unit last = _trackFollowTarget;
        if (IsTrackable(last)) return last;
        if (_trackAutoSwitchToEarlier)
        {
            Unit earlier = PeekEarlierTrackable();
            if (earlier != null) return earlier;
        }
        return null;
    }

    Unit PickPrimaryOrFirst(List<Unit> candidates)
    {
        Unit primary = _selection?.Primary;
        if (primary != null)
            for (int i = 0; i < candidates.Count; i++)
                if (ReferenceEquals(candidates[i], primary)) return primary;
        return candidates.Count > 0 ? candidates[0] : null;
    }

    bool IsTrackable(Unit unit)
    {
        if (unit == null || !unit.Alive) return false;
        if (!_trackAllowBuildings && unit.IsBuilding) return false;
        if (unit.Player == ViewPlayer) return true;
        if (IsNeutralSlot(unit.Player)) return _trackAllowNeutral;
        return AreAllied(unit.Player, ViewPlayer) ? _trackAllowAlly : _trackAllowEnemy;
    }

    void PushTrackStack(Unit unit)
    {
        if (!_trackAutoSwitchToEarlier || unit == null) return;
        List<Unit> stack = _trackStack;
        int existing = stack.FindIndex(candidate => ReferenceEquals(candidate, unit));
        if (existing >= 0) stack.RemoveAt(existing);
        if (_trackHeroTopInStack && unit.IsHero)
        {
            stack.Add(unit);
        }
        else
        {
            int at = _trackHeroTopInStack
                ? stack.FindIndex(candidate => candidate?.IsHero == true) : -1;
            if (at < 0) stack.Add(unit);
            else stack.Insert(at, unit);
        }
        PruneTrackStack();
    }

    Unit PeekEarlierTrackable()
    {
        PruneTrackStack();
        List<Unit> stack = _trackStack;
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            Unit candidate = stack[i];
            if (ReferenceEquals(candidate, _trackFollowTarget)) continue;
            if (IsTrackable(candidate)) return candidate;
        }
        return null;
    }

    void PruneTrackStack()
    {
        List<Unit> stack = _trackStack;
        for (int i = stack.Count - 1; i >= 0; i--)
            if (!IsTrackable(stack[i])) stack.RemoveAt(i);
    }

    // ------------------------------------------------------------------
    //  标记 + 跟随标记单位
    // ------------------------------------------------------------------

    /// <summary>把当前聚焦的单位记成「标记单位」并开始跟随（再按一次取消跟随，标记保留）。</summary>
    public void ToggleCameraFollowMarked()
    {
        if (_markedFollowActive)
        {
            _markedFollowActive = false;
            _camera?.ClearSoftFollowController();
            _log?.Add("镜头跟随标记单位：关");
            return;
        }
        Unit focused = PrimarySelection;
        if (focused != null && focused.Alive) _markedFollowUnit = focused;
        if (_markedFollowUnit == null || !_markedFollowUnit.Alive)
        {
            _log?.Add("还没有标记任何镜头跟随单位");
            return;
        }
        _lockFollowActive = false;
        _trackFollowActive = false;
        _markedFollowActive = true;
        _followSampleUnit = null;
        _log?.Add($"镜头跟随标记单位：{_markedFollowUnit.Type?.Name ?? "单位"}");
    }

    /// <summary>
    /// 总跟随的过滤开关（对应客户端安卓面板那几个复选框，Launcher 暂无面板）。
    /// 默认值就是客户端默认：建筑参与，敌人/中立/友军都不参与；自己的单位永远允许。
    /// </summary>
    public void SetCameraFollowTrackFilter(bool allowBuildings, bool allowEnemy,
        bool allowNeutral, bool allowAlly)
    {
        _trackAllowBuildings = allowBuildings;
        _trackAllowEnemy = allowEnemy;
        _trackAllowNeutral = allowNeutral;
        _trackAllowAlly = allowAlly;
    }

    void UpdateMarkedFollow(float deltaTime)
    {
        if (!_markedFollowActive) return;
        Unit target = _markedFollowUnit;
        if (target == null)
        {
            _markedFollowActive = false;
            return;
        }
        if (!target.Alive)
        {
            _markedFollowActive = false;
            _camera?.ClearSoftFollowController();
            _log?.Add("曾标记的单位已不在地图之中");
            return;
        }
        if (CameraFollowYielded || ShouldReleaseCameraForPlayerPan)
        {
            _camera?.ClearSoftFollowController();
            return;
        }
        _camera.SetSoftFollowController(target, AdvanceFollowSample(target, deltaTime));
    }

    // ------------------------------------------------------------------
    //  渲染率样本
    // ------------------------------------------------------------------

    /// <summary>
    /// 把上一次的样本按单位速度朝仿真坐标推进一步（客户端 RenderPosition 的追帧逻辑：
    /// 20Hz 仿真每 tick 跳一次，样本每渲染帧吃掉 speed*dt，于是高帧率下是连续的）。
    /// 换目标、瞬移、速度为 0 时直接贴合。
    /// </summary>
    Vector3 AdvanceFollowSample(Unit unit, float deltaTime)
    {
        Vector3 target = unit.Position;
        if (!ReferenceEquals(_followSampleUnit, unit))
        {
            _followSampleUnit = unit;
            _followSamplePosition = target;
            return target;
        }
        float speed = unit.MoveSpeedBase;
        float step = speed * MathF.Min(deltaTime, World.TickSeconds * 2f);
        float dx = target.X - _followSamplePosition.X;
        float dz = target.Z - _followSamplePosition.Z;
        float distance = MathF.Sqrt(dx * dx + dz * dz);
        // 掉帧/瞬移/停步都直接贴合：超过 64 单位（或 3 步）就不是"追赶"而是"落后"了，
        // 与客户端 SnapRenderTransform 的判据同源（max(64, expectedStep*3)）。
        bool snap = speed <= 0f || deltaTime >= 1f || distance <= step || distance <= 1e-3f
            || distance > MathF.Max(64f, step * 3f);
        if (snap)
        {
            _followSamplePosition = target;
        }
        else
        {
            float k = step / distance;
            _followSamplePosition = new Vector3(_followSamplePosition.X + dx * k,
                target.Y, _followSamplePosition.Z + dz * k);
        }
        return _followSamplePosition;
    }
}
