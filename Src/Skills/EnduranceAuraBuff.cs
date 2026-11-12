namespace OpenWC3;

public sealed class EnduranceAuraBuff : AuraBuff
{
    public float MoveSpeedPct;      // 移动速度加成(比例, 0.1 = +10%)
    public float AttackSpeedPct;    // 攻击速度加成(比例, 0.05 = +5%)
    float _appliedSpeed;
    float _appliedAttack;
    double _remaining=4;

    public override string Name => "耐久";

    protected override void OnBuffAdd()
    {
        // 原版耐久光环按基础移速的百分比加成, 不是固定值。
        RefreshModifiers();
    }

    void RefreshModifiers()
    {
        float speed=Owner.Stats.MoveSpeedBase*MoveSpeedPct;
        Owner.Stats.SpeedBonus+=speed-_appliedSpeed;
        Owner.Stats.AttackSpeedBonusPct+=AttackSpeedPct-_appliedAttack;
        _appliedSpeed=speed;_appliedAttack=AttackSpeedPct;
    }

    public bool RefreshFrom(EnduranceAura aura,int level,float move,float attack)
    {
        const float epsilon=.001f;
        // Original 4c9890 rejects only if both incoming components are weaker.
        if(move<MoveSpeedPct&&MathF.Abs(move-MoveSpeedPct)>=epsilon
            &&attack<AttackSpeedPct&&MathF.Abs(attack-AttackSpeedPct)>=epsilon)return false;
        _remaining=4f;Source=aura.Owner;SourceAura=aura;AuraLevel=level;
        if(move>MoveSpeedPct&&MathF.Abs(move-MoveSpeedPct)>=epsilon)MoveSpeedPct=move;
        if(attack>AttackSpeedPct&&MathF.Abs(attack-AttackSpeedPct)>=epsilon)AttackSpeedPct=attack;
        RefreshModifiers();return true;
    }

    public override void OnTick(float dt)
    {
        RefreshModifiers();
        _remaining-=dt;
        if(_remaining<=.000001f)Owner.RemoveAbility(this);
    }

    protected override void OnBuffRemove()
    {
        Owner.Stats.SpeedBonus -= _appliedSpeed;
        Owner.Stats.AttackSpeedBonusPct -= _appliedAttack;
        _appliedSpeed = _appliedAttack = 0f;
    }
}
