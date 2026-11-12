namespace OpenWC3;

// 虔诚光环 (对标 CAbilityAuraDevotion): 给盟友加护甲 Buff
public sealed class DevotionAura : AuraAbility
{
    public override string Name => "虔诚光环";
    public DevotionAura() { OrderId = "devotion"; Learnable = true; MaxLevel = 3; Level = 0; AffectsAllies = true; Radius = 900f; }
    // 原版 AHad: Area=900(三等级恒定), DataA=护甲加成 1.5/3/4.5(浮点)。
    //
    // ★ fallback 取中性值 0: 物编实测 Area1~4 均为 900, 读得到就不走 fallback,
    //   改 0 行为不变。Radius=0 时光环不生效(AuraAbility 半径 0 = 不扫描目标),
    //   于是"物编缺 Area"从"半径被静默填成 900"变成"光环不生效 + 一条
    //   [物编缺字段] 日志" —— 后者能立刻定位是哪个 rawcode 的Area 没配。
    public float ArmorBonus => GetLevelFloat("Data", 1, 0f);
    public override void OnTick(float dt)
    {
        Radius = GetLevelFloat("Area", 0, 0f);
        base.OnTick(dt);
    }
    protected override AuraBuff CreateBuff(Unit source, Unit target, int level)
    {
        // 514410 merge writes a constant 4s decay window regardless of rank.
        var buff = new DevotionAuraBuff(ArmorBonus)
        {
            Source = source,
            Duration = 4f,
            Rawcode = GetLevelString("BuffID", 0, "BHad") is { Length: >= 4 } id ? id[..4] : "BHad"
        };
        return buff;
    }
    // 同 BuffID 全局单实例: 任意来源的 BHad 都参与 514410 三态合并,
    // 而不是每个圣骑士各挂一个 buff 靠 NonStack 取最大兜底。
    // Index access keeps the previous OfType/FirstOrDefault first-match order and
    // the per-candidate BuffID fallback evaluation, without boxing the list
    // enumerator on every scanned target.
    protected override AuraBuff FindExistingBuff(Unit target)
    {
        IReadOnlyList<Ability> abilities=target.Abilities.Items;
        for(int i=0;i<abilities.Count;i++)
            if(abilities[i] is DevotionAuraBuff buff&&buff.Rawcode==
                (GetLevelString("BuffID",0,"BHad") is {Length:>=4} id?id[..4]:"BHad"))
                return buff;
        return null;
    }
    protected override bool RefreshExistingBuff(AuraBuff buff,int level)
    {
        ((DevotionAuraBuff)buff).RefreshFrom(this,level);
        // 被拒绝的弱输入必须保留强 buff 的剩余计时。
        return true;
    }
}
