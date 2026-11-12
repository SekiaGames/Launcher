namespace OpenWC3;

// 可调度的时限效果 (对标 CAgent/CEffect/CMissile): Step 返回 false 表示结束并被移除
public abstract class Effect
{
    public abstract bool Step(World w, float dt);
    public virtual Vec2 Pos => default;        // 导出/可视化用位置
    public virtual string VKind => "effect";   // 可视化分类
    public virtual float VRadius => 0;         // 可视化半径(特效范围)
    // Authored spell visuals can suppress the fallback ring without losing
    // their area metadata for diagnostics and exports.
    public virtual bool ShowAreaOverlay => true;
    public virtual string VisualModelPath => "";
    public virtual Unit VisualTarget => null;
    public virtual string VisualAttachmentPoint => "origin";
    public virtual float VisualHeight => 0;
    public virtual float VisualScale => 1;
    public virtual float VisualAgeSeconds => 0;
}

/// <summary>
/// 统一的投射物表现契约。普通攻击弹道和技能弹道共用渲染路径，
/// 但各自保留独立的命中结算逻辑。
/// </summary>
public abstract class MissileEffect : Effect
{
    Vec2 _previousPos;
    float _previousProgress;
    float _previousArcOffset;
    float _previousAge;
    bool _hasPreviousState;

    public abstract float TravelProgress { get; }
    public abstract float ArcOffset { get; }
    public abstract Vec2 SegmentLaunchPos { get; }
    public abstract Vec2 SegmentTargetPos { get; }
    public abstract float LaunchHeight { get; }
    public abstract float ImpactHeight { get; }
    public abstract float Age { get; }
    public abstract Vec2 Direction { get; }
    public abstract int Team { get; }
    public abstract string MissileArt { get; }

    internal void CapturePreviousState()
    {
        _previousPos = Pos;
        _previousProgress = TravelProgress;
        _previousArcOffset = ArcOffset;
        _previousAge = Age;
        _hasPreviousState = true;
    }

    public Vec2 RenderPos(float alpha)
    {
        if (!_hasPreviousState) return Pos;
        alpha = Math.Clamp(alpha, 0f, 1f);
        return _previousPos + (Pos - _previousPos) * alpha;
    }

    public float RenderTravelProgress(float alpha) => !_hasPreviousState
        ? TravelProgress
        : float.Lerp(_previousProgress, TravelProgress,
            Math.Clamp(alpha, 0f, 1f));

    public float RenderArcOffset(float alpha) => !_hasPreviousState
        ? ArcOffset
        : float.Lerp(_previousArcOffset, ArcOffset,
            Math.Clamp(alpha, 0f, 1f));

    public float RenderAge(float alpha) => !_hasPreviousState
        ? Age
        : float.Lerp(_previousAge, Age, Math.Clamp(alpha, 0f, 1f));

    public virtual float VisualPitch(float startingWorldZ, float targetWorldZ)
        => VisualPitch(startingWorldZ, targetWorldZ, TravelProgress);

    public virtual float VisualPitch(float startingWorldZ, float targetWorldZ,
        float progress)
    {
        float distance = MathF.Max(1f,
            Vec2.Dist(SegmentLaunchPos, SegmentTargetPos));
        float linearSlope = (targetWorldZ - startingWorldZ) / distance;
        float arcSlope = 4f * ArcHeightRatio * (1f - 2f * progress);
        return MathF.Atan(linearSlope + arcSlope);
    }

    protected virtual float ArcHeightRatio => 0f;
    public override string VKind => "missile";
}
