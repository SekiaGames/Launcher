namespace OpenWC3;

/// <summary>
/// 技能专用追踪弹道。只负责确定性移动与到达通知，不经过普通攻击、
/// 武器加成或法球管线，避免技能伤害被错误叠加攻击力。
/// </summary>
public sealed class AbilityMissileEffect : MissileEffect
{
    readonly Unit _source;
    readonly Unit _target;
    readonly string _missileArt;
    readonly float _speed;
    readonly float _arcHeight;
    readonly float _launchHeight;
    readonly float _impactHeight;
    readonly bool _homing;
    readonly bool _pointTarget;
    readonly float _visualScale;
    readonly Action<World, Unit> _onImpact;
    readonly Vec2 _launchPosition;
    readonly Vec2 _targetAtLaunch;
    readonly float _totalDistance;
    Vec2 _position;
    Vec2 _direction;
    float _travelled;
    float _age;

    public AbilityMissileEffect(Unit source, Unit target, string missileArt,
        float speed, float arcHeight, Action<World, Unit> onImpact,
        float? launchHeight = null, float? impactHeight = null,
        bool homing = true, float visualScale = 1f)
    {
        _source = source;
        _target = target;
        _missileArt = missileArt ?? "";
        _speed = MathF.Max(1f, speed);
        _arcHeight = MathF.Max(0f, arcHeight);
        _launchHeight = launchHeight
            ?? ((source?.FlyHeight ?? 0f) + (source?.ProjectileLaunchZ ?? 80f));
        _impactHeight = impactHeight
            ?? ((target?.FlyHeight ?? 0f) + (target?.ProjectileImpactZ ?? 60f));
        _homing = homing;
        _visualScale = MathF.Max(0f, visualScale);
        _onImpact = onImpact;
        _position = source?.Pos ?? default;
        _launchPosition = _position;
        _targetAtLaunch = target?.Pos ?? _position;
        _totalDistance = MathF.Max(1f,
            Vec2.Dist(_launchPosition, _targetAtLaunch));
        _direction = (_targetAtLaunch - _launchPosition).Normalized;
    }

    public AbilityMissileEffect(Unit source,Vec2 point,string missileArt,
        float speed,float arcHeight,Action<World> onImpact,
        float? launchHeight=null,float? impactHeight=null,float visualScale=1f)
        :this(source,null,missileArt,speed,arcHeight,(world,_)=>onImpact?.Invoke(world),
            launchHeight,impactHeight??0,homing:false,visualScale:visualScale)
    {
        _pointTarget=true;
        _targetAtLaunch=point;
        _totalDistance=MathF.Max(1f,Vec2.Dist(_launchPosition,point));
        _direction=(point-_launchPosition).Normalized;
    }

    public override bool Step(World world, float dt)
    {
        _age += dt;
        if (!_pointTarget&&(_target?.Alive != true||_target.State.Is(StateModType.Phased))) return false;
        Vec2 targetPosition = !_pointTarget&&_homing ? _target.Pos : _targetAtLaunch;
        Vec2 toTarget = targetPosition - _position;
        float step = _speed * MathF.Max(0f, dt);
        if (toTarget.Length > step)
        {
            _direction = toTarget.Normalized;
            _position += _direction * step;
            _travelled += step;
            return true;
        }

        _position = targetPosition;
        _travelled += toTarget.Length;
        _onImpact?.Invoke(world, _target);
        return false;
    }

    public override float TravelProgress
    {
        get
        {
            float remaining = _target == null&&!_pointTarget
                ? 0f : Vec2.Dist(_position,
                    _homing ? _target.Pos : _targetAtLaunch);
            return Math.Clamp(_travelled
                / MathF.Max(1f, _travelled + remaining), 0f, 1f);
        }
    }

    public override float ArcOffset
    {
        get
        {
            float progress = TravelProgress;
            return 4f * (_arcHeight * _totalDistance)
                * progress * (1f - progress);
        }
    }

    protected override float ArcHeightRatio => _arcHeight;
    public override Vec2 SegmentLaunchPos => _launchPosition;
    public override Vec2 SegmentTargetPos => _targetAtLaunch;
    public override float LaunchHeight => _launchHeight;
    public override float ImpactHeight => _impactHeight;
    public override float Age => _age;
    public override Vec2 Direction => _direction;
    public override int Team => _source?.Team ?? 0;
    public override string MissileArt => _missileArt;
    public override float VisualScale => _visualScale;
    public override Vec2 Pos => _position;
}
