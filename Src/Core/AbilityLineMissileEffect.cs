namespace OpenWC3;

/// <summary>
/// 技能专用定向直线弹道。每帧报告本帧扫过的距离区间，命中形状和结算规则
/// 由技能实现决定；表现仍复用统一 MissileEffect 渲染路径。
/// </summary>
public sealed class AbilityLineMissileEffect : MissileEffect
{
    readonly Unit _source;
    readonly string _missileArt;
    readonly Vec2 _launchPosition;
    readonly Vec2 _targetPosition;
    readonly Vec2 _direction;
    readonly float _distance;
    readonly float _speed;
    readonly float _launchHeight;
    readonly float _impactHeight;
    readonly float _visualScale;
    readonly Action<World, float, float> _onSweep;
    Vec2 _position;
    float _travelled;
    float _age;

    public AbilityLineMissileEffect(Unit source, Vec2 direction, float distance,
        string missileArt, float speed, Action<World, float, float> onSweep,
        float launchHeight = 80f, float impactHeight = 60f,
        float visualScale = 1f)
    {
        _source=source;
        _missileArt=missileArt??"";
        _launchPosition=source?.Pos??default;
        _direction=direction.Length>.001f?direction.Normalized:new Vec2(1,0);
        _distance=MathF.Max(0,distance);
        _speed=MathF.Max(1,speed);
        _launchHeight=launchHeight;
        _impactHeight=impactHeight;
        _visualScale=MathF.Max(0,visualScale);
        _onSweep=onSweep;
        _position=_launchPosition;
        _targetPosition=_launchPosition+_direction*_distance;
    }

    public override bool Step(World world,float dt)
    {
        _age+=MathF.Max(0,dt);
        float previous=_travelled;
        _travelled=MathF.Min(_distance,
            _travelled+_speed*MathF.Max(0,dt));
        _position=_launchPosition+_direction*_travelled;
        _onSweep?.Invoke(world,previous,_travelled);
        return _travelled<_distance;
    }

    public override float TravelProgress=>_distance<=.001f
        ?1f:Math.Clamp(_travelled/_distance,0f,1f);
    public override float ArcOffset=>0;
    protected override float ArcHeightRatio=>0;
    public override Vec2 SegmentLaunchPos=>_launchPosition;
    public override Vec2 SegmentTargetPos=>_targetPosition;
    public override float LaunchHeight=>_launchHeight;
    public override float ImpactHeight=>_impactHeight;
    public override float Age=>_age;
    public override Vec2 Direction=>_direction;
    public override int Team=>_source?.Team??0;
    public override string MissileArt=>_missileArt;
    public override float VisualScale=>_visualScale;
    public override Vec2 Pos=>_position;
}
