namespace OpenWC3;
using System.Numerics;

/// <summary>
/// Unified runtime visual effect used by JASS AddSpecialEffect, abilities and buffs.
/// It deliberately remains a simulation Effect so creation/destruction is deterministic.
/// </summary>
public sealed class SpecialEffect : Effect
{
    readonly string _modelPath;
    readonly Unit _target;
    readonly string _attachmentPoint;
    Vec2 _position;
    float _remaining;
    float _age;
    bool _expired;

    public SpecialEffect(string modelPath, Vec2 position, float lifeSpan = 0,
        float scale = 1, float height = 0)
    {
        _modelPath = modelPath ?? "";
        _position = position;
        _remaining = lifeSpan;
        LifeSpan = lifeSpan;
        Scale = scale;
        Height = height;
    }

    public SpecialEffect(string modelPath, Unit target, string attachmentPoint = "origin",
        float lifeSpan = 0, float scale = 1)
        : this(modelPath, target?.Pos ?? default, lifeSpan, scale)
    {
        _target = target;
        _attachmentPoint = attachmentPoint;
    }

    public float LifeSpan { get; }
    public float Scale { get; set; } = 1;
    public bool PlayStandAfterBirth { get; set; }
    // Native SPN children use 76c6f0 rather than the general model-event
    // callback: sounds remain enabled, nested spawn/splat events do not.
    public bool SoundOnlyModelEvents { get; set; }
    public float Height { get; set; }
    public Vector4 Color { get; set; } = Vector4.One;
    // World-space effects have no target from which rendering can infer player color.
    // A negative value means "inherit from VisualTarget, otherwise player color 0".
    public int TeamColorIndex { get; set; } = -1;
    // Blz 扩展: 朝向与时间。此前这些 native 完全未注册, 地图调用会拿到未注册函数。
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public float Roll { get; set; }
    // YDWE composes a model-space matrix, independently of uniform size.
    public Matrix4x4 JapiTransform { get; set; } = Matrix4x4.Identity;
    public string AnimationName { get; set; }
    public HashSet<string> AnimationSubProperties {get;}=new(StringComparer.OrdinalIgnoreCase);
    public bool HasAbsoluteHeight { get; set; }
    bool _positionOverride;
    // 动画时间覆盖: null 时按 _age 自然推进, 非空即 BlzSetSpecialEffectTime 指定的时刻。
    // D-FX-02: 这是 **seek 而非 pause** —— 原版设置播放位置后动画继续推进。
    // 取证不能套用音效侧的"seek 不干预状态机"结论(War3AudioSystem.cs:410-424 已明确
    // 那是 Miles 音频子系统专属); 特效侧的独立依据是 Reforged jassdoc 对
    // BlzSetSpecialEffectTimeScale 的说明(lep.duckdns.org/jassbot, patch 1.29.2.9231):
    // "if you however set it to -100.0 it will go backwards and reset towards the
    // beginning ... if you want to reset the animation, you must pass it a negative
    // value mid animation, else it will stand still."
    // 即动画时间是一个**持续推进的状态量**(可被 seek 改写起点, 但不停止推进),
    // 因此不能把 AnimationTime 做成常量。setter 记录 seek 发生时的 _age 作为基准,
    // 由 VisualAgeSeconds 做 "基准 + 已流逝" 的偏移。
    public float? AnimationTime { get => _animationTime; set
    {
        if (value == _animationTime) return;
        _animationTime = value;
        // 负值不改变播放位置(见 jassdoc: 负 TimeScale 不生效, 只是 stand still),
        // 故只在给定非负 seek 目标时记录基准, 避免把基准推到 _age 之前。
        if (value is null or >= 0) _ageAtAnimationTimeSet = _age;
    } }
    float? _animationTime;
    float _ageAtAnimationTimeSet;
    // D-FX-03: BlzSetSpecialEffectTimeScale。权威语义见 Reforged jassdoc
    // (lep.duckdns.org/jassbot/doc/BlzSetSpecialEffectTimeScale, patch 1.29.2.9231):
    // "if you see the animation speed at 100.0 it will go at 100% speed" ——
    // 即**以 100 为正常速率基准**(不是 1.0), 负值倒放, 且负值不生效只是 stand still。
    // null = 未设置 = 正常速率 100。
    public float? TimeScale { get; set; }
    public bool Destroyed { get; private set; }
    float _deathRemaining;
    public float DeathAge { get; private set; }
    // 对标原版 DestroyEffect: 特效播完 Death 序列才消失, 不是立即从世界移除。
    // DotA 大量用 DestroyEffect(AddSpecialEffect(...)) 模式, 立即移除会让特效只存在 1 帧。
    const float DeathFade = 1.2f;
    public void Destroy()
    {
        if (Destroyed) return;
        Destroyed = true;
        _deathRemaining = DeathFade;
    }
    // 渲染层兜底: lifeSpan=0 的非循环特效动画播完后立即回收, 避免永久残留。
    public void Expire() => _expired = true;
    public void SetPosition(Vec2 position,bool detachFromTarget=false)
    { _position=position;_positionOverride|=detachFromTarget; }
    public Matrix4x4 ApplyJapiTransform(Matrix4x4 model)
    {
        model=JapiTransform*model;
        if(HasAbsoluteHeight)model.M43=Height;
        return model;
    }

    public override bool Step(World w, float dt)
    {
        if (_expired) return false;
        // D-FX-03: 特效时间轴按 TimeScale 推进。基准 100 = 正常速率(jassdoc 1.29.2.9231
        // "if you see the animation speed at 100.0 it will go at 100% speed")。
        // TimeScale 未设置 = 正常速率(1.0); 负值按 0 处理 —— jassdoc 明确负值
        // "can't start at a negative value ... else it will stand still",
        // 即**不生效**而不是反向累加(反向累加会让特效跳出动画起点进入未定义区)。
        float scale=1f;
        if(TimeScale is { } ts)scale=ts>0f?ts/100f:0f;
        _age += dt * scale;
        if (_target != null && !_target.Alive) return false;
        if (Destroyed)
        {
            DeathAge += dt;
            _deathRemaining -= dt;
            return _deathRemaining > 0;
        }
        if (LifeSpan <= 0) return true;
        _remaining -= dt;
        return _remaining > 0;
    }

    public override Vec2 Pos => !_positionOverride&&_target!=null?_target.Pos:_position;
    public override string VisualModelPath => _modelPath;
    public override Unit VisualTarget => _positionOverride?null:_target;
    public override string VisualAttachmentPoint => _attachmentPoint ?? "origin";
    public override float VisualHeight => Height;
    public override float VisualScale => Scale;
    // D-FX-02: seek 后继续走 —— 返回 "seek 基准 + seek 之后已流逝的时间",
    // 而非把 AnimationTime 当常量(那会让特效冻结在指定帧)。
    public override float VisualAgeSeconds => _animationTime is { } seek
        ? seek + (_age - _ageAtAnimationTimeSet)
        : _age;
}
