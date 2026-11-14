using Godot;

namespace OpenWC3;

/// <summary>
/// 单位生命周期：死亡 → 尸体 → 英雄复活。对标客户端 Units/Unit.Lifecycle.cs 与 Components/Death.cs。
/// </summary>
public sealed partial class Unit
{
    public bool IsCorpse;
    public float CorpseRemaining;

    public float DeathAnimTime = 0.5f;
    public float DecayTime = 2f;
    public float BoneDecayTime = 88f;
    public float DissipateTime = 3f;

    public float DeathElapsed;
    public bool IsBone;
    public bool DeathPresentationGone;

    public float PresentationAlpha => IsHero && !Alive && DeathElapsed >= DeathAnimTime
        ? Mathf.Clamp((DissipateTime - (DeathElapsed - DeathAnimTime)) / 2.5f, 0f, 1f)
        : 1f;
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
        ConfigDeathTiming();
        CorpseRemaining = IsCorpse ? DecayLife : 0f;
        if (IsBuilding && !IsIllusion && View is { FogFrozen: true })
            DeathPresentationGone = true;
        else
            View?.Play("Death");
        View?.SetSelected(false);
    }

    private void ConfigDeathTiming()
    {
        War3GameplayConfig config = War3GameplayConfig.Default;
        DeathAnimTime = Type != null && Type.DeathAnimTime > 0f ? Type.DeathAnimTime : 0f;
        DecayTime = Type == null || Type.CorpseDecays ? config.DecayTime : 0f;
        BoneDecayTime = Type == null || Type.CorpseDecays ? config.BoneDecayTime : 0f;
        DissipateTime = config.DissipateTime;
        DeathElapsed = 0f;
        IsBone = false;
        DeathPresentationGone = false;
    }

    public float DecayLife => DeathAnimTime + DecayTime + BoneDecayTime;

    public void TickCorpse(float delta)
    {
        if (DeathPresentationGone) return;
        if (CorpseCarrier != null) return;
        if (CorpseDecaySuspended) return;

        DeathElapsed += delta;

        if (IsHero)
        {
            if (DeathElapsed < DeathAnimTime + DissipateTime) return;
            DeathPresentationGone = true;
            IsCorpse = false;
            if (View != null) View.Root.Visible = false;
            return;
        }

        float boneAt = DeathAnimTime + DecayTime;
        if (!IsBone && DeathElapsed >= boneAt) IsBone = true;

        float total = DecayLife;
        CorpseRemaining = Mathf.Max(0f, total - DeathElapsed);
        if (DeathElapsed < total) return;

        IsCorpse = false;
        DeathPresentationGone = true;
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
        ResetDeathPresentation();
        Hp = Mathf.Max(1f, MaxHp * Mathf.Clamp(lifeFactor, 0.01f, 1f));
        Position = position;
        Home = position;
        State.Clear();

        if (View != null)
        {
            View.Root.Visible = true;
            View.Root.Position = position;
            View.Play("Stand");
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
        ConfigDeathTiming();
        DecayTime = 0f;
        BoneDecayTime = 2f;
        CorpseRemaining = DecayLife;
        View?.SetSelected(false);
        View?.Play("Death");
    }

    public void ResetDeathPresentation()
    {
        DeathElapsed = 0f;
        IsBone = false;
        DeathPresentationGone = false;
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
        ResetDeathPresentation();
        Hp = MaxHp * War3GameplayConfig.Default.HeroReviveLifeFactor;
        Mana = Mathf.Max(Mana, MaxMana * War3GameplayConfig.Default.HeroReviveManaStart);
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
        }
    }
}
