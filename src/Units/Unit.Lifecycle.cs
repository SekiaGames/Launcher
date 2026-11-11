using Godot;

namespace Launcher;

/// <summary>
/// 单位生命周期：死亡 → 尸体 → 英雄复活。对标客户端 Units/Unit.Lifecycle.cs 与 Components/Death.cs。
/// </summary>
public sealed partial class Unit
{
    /// <summary>尸体停留时长（对标 Misc 的 88 秒衰减，建筑不留尸体）。</summary>
    public const float CorpseSeconds = 88f;

    public bool IsCorpse;
    public float CorpseRemaining;

    /// <summary>英雄阵亡后等待复活（没钱或没祭坛时保持这个状态）。</summary>
    public bool AwaitingRevive;
    public float ReviveRemaining;
    public float ReviveTotal;
    public int ReviveGold;
    public Unit ReviveAltar;
    /// <summary>没钱/没祭坛时的重试节流，避免每 tick 都去查金库。</summary>
    public float ReviveRetry;

    public bool CanBeRevived => IsHero && !Alive;

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
        IsCorpse = !IsBuilding;
        CorpseRemaining = IsCorpse ? CorpseSeconds : 0f;
        View?.Play("Death");
        View?.SetHp(0f, false);
        View?.SetSelected(false);
    }

    /// <summary>尸体到期后从画面上消失，但对象保留（英雄还要靠它复活，等级与经验不能丢）。</summary>
    public void TickCorpse(float delta)
    {
        if (!IsCorpse) return;

        CorpseRemaining -= delta;
        if (CorpseRemaining > 0f) return;

        IsCorpse = false;
        if (View != null) View.Root.Visible = false;
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
