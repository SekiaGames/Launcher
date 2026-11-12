using Godot;

namespace OpenWC3;

/// <summary>
/// 幻象生成。对标 Wiki RuntimeMirrorImageAbility 的 UnitFactory.SetIllusion 那条：
/// 用同一个单位类型在旁边复制一个，标记幻象并抄本体的属性/生命比例，
/// 存续期走召唤计时（到期直接消散，不留尸），死亡不给经验/赏金。
/// </summary>
public sealed partial class World
{
    public Unit CreateIllusion(Unit source, Vector3 position, float seconds, float dealtFactor, float receivedFactor)
    {
        if (source?.Type == null) return null;

        Unit image = Summon(source, source.Type.Id, position, Mathf.Max(World.FrameSeconds, seconds));
        if (image == null) return null;

        image.IsIllusion = true;
        image.IllusionDamageDealt = Mathf.Clamp(dealtFactor, 0f, 5f);
        image.IllusionDamageReceived = Mathf.Max(0f, receivedFactor);
        image.IsHero = false;
        image.Level = source.Level;
        image.Strength = source.Strength;
        image.Agility = source.Agility;
        image.Intelligence = source.Intelligence;
        image.BonusArmor = source.BonusArmor;
        image.BonusDamageFlat = source.BonusDamageFlat;
        image.BonusMoveSpeedFlat = source.BonusMoveSpeedFlat;
        image.MaxHp = source.MaxHp;
        image.Hp = Mathf.Clamp(source.Hp, 1f, image.MaxHp);
        image.MaxMana = source.MaxMana;
        image.Mana = source.Mana;
        image.Facing = source.Facing;
        image.View?.SetHp(image.Hp / Mathf.Max(1f, image.MaxHp), true);
        return image;
    }

    public void DismissIllusion(Unit image)
    {
        if (image == null || !image.Alive) return;
        KillUnit(image);
        image.IsCorpse = false;
        if (image.View != null) image.View.Root.Visible = false;
    }
}