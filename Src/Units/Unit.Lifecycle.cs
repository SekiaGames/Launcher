using Godot;

namespace OpenWC3;

/// <summary>
/// 单位生命周期：死亡 → 尸体 → 英雄复活。对标客户端 Units/Unit.Lifecycle.cs 与 Components/Death.cs。
/// </summary>
public sealed partial class Unit
{
    /// <summary>尸体停留时长（对标 Misc 的 88 秒衰减，建筑不留尸体）。</summary>
    public const float CorpseSeconds = 88f;

    public bool IsCorpse;
    public float CorpseRemaining;
    /// <summary>尸体被绞肉车装进货舱时指向携带者：非空时尸体不衰减、模型隐藏、位置跟随携带者。</summary>
    public Unit CorpseCarrier;

    /// <summary>英雄阵亡后等待复活（没钱或没祭坛时保持这个状态）。</summary>
    public bool AwaitingRevive;
    public float ReviveRemaining;
    public float ReviveTotal;
    public int ReviveGold;
    public Unit ReviveAltar;
    /// <summary>没钱/没祭坛时的重试节流，避免每 tick 都去查金库。</summary>
    public float ReviveRetry;

    public bool CanBeRevived => IsHero && !Alive;

    /// <summary>召唤物剩余存在时间（秒）；&lt;= 0 表示不是召唤物。</summary>
    public float SummonRemaining;
    public Unit Summoner;
    public bool IsSummon => SummonRemaining > 0f;

    /// <summary>进入死亡：清空指令与状态，留下尸体（建筑不留）。</summary>
    public void EnterDeath()
    {
        Alive = false;
        Hp = 0f;
        Target = null;
        MoveTarget = null;
        GoldMine = null;
        Tree = null;
        BuildSite = null;
        BuildingNow = false;
        Carry = 0;
        CarryLumber = 0;
        Path = null;
        PathIndex = 0;
        RepathTimer = 0f;
        State.Clear();
        IsUnderConstruction = false;
        ReviveRemaining = 0f;
        ReviveAltar = null;
        AwaitingRevive = IsHero;
        IsCorpse = !IsBuilding && !IsIllusion;
        CorpseRemaining = !IsCorpse ? 0f : Type.CorpseDecays ? CorpseSeconds : float.PositiveInfinity;
        View?.Play("Death");
        View?.SetHp(0f, false);
        View?.SetSelected(false);
    }

    /// <summary>尸体到期后从画面上消失，但对象保留（英雄还要靠它复活，等级与经验不能丢）。</summary>
    public void TickCorpse(float delta)
    {
        if (!IsCorpse) return;
        if (CorpseCarrier != null) return;

        CorpseRemaining -= delta;
        if (CorpseRemaining > 0f) return;

        IsCorpse = false;
        if (View != null) View.Root.Visible = false;
    }

    /// <summary>复活一个尸体（复活技能用）：满血起身，不再是尸体。</summary>
    public void ReviveFromCorpse(Vector3 position, float lifeFactor = 1f)
    {
        CorpseCarrier = null;
        Alive = true;
        IsCorpse = false;
        CorpseRemaining = 0f;
        AwaitingRevive = false;
        ReviveRemaining = 0f;
        ReviveAltar = null;
        Hp = Mathf.Max(1f, MaxHp * Mathf.Clamp(lifeFactor, 0.01f, 1f));
        Position = position;
        Home = position;
        State.Clear();

        if (View != null)
        {
            View.Root.Visible = true;
            View.Root.Position = position;
            View.Play("Stand");
            View.SetHp(Hp / Mathf.Max(1f, MaxHp), true);
        }
    }

    /// <summary>召唤物到期：直接散掉，不留尸、不给经验、不掉落。</summary>
    public void ExpireSummon()
    {
        Alive = false;
        Hp = 0f;
        SummonRemaining = 0f;
        Target = null;
        MoveTarget = null;
        Path = null;
        State.Clear();
        IsCorpse = true;
        CorpseRemaining = 2f;
        View?.SetSelected(false);
        View?.SetHp(0f, false);
        View?.Play("Death");
    }

    /// <summary>按 HeroReviveLifeFactor 与 HeroReviveManaStart/Factor 恢复上场。</summary>
    public void ReviveAt(Vector3 position, float yaw)
    {
        Alive = true;
        IsCorpse = false;
        CorpseRemaining = 0f;
        AwaitingRevive = false;
        ReviveRemaining = 0f;
        ReviveAltar = null;
        Hp = MaxHp * War3GameplayConfig.HeroReviveLifeFactor;
        Mana = Mathf.Max(Mana, MaxMana * War3GameplayConfig.HeroReviveManaStart);
        Position = position;
        Home = position;
        Facing = yaw;
        TargetFacing = yaw;
        State.Clear();

        if (View != null)
        {
            View.Root.Visible = true;
            View.Root.Position = position;
            View.Root.Rotation = new Vector3(0f, yaw, 0f);
            View.Play("Stand");
            View.SetHp(Hp / Mathf.Max(1f, MaxHp), true);
        }
    }
}
