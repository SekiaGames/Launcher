using Godot;
using System;

namespace Launcher;

public static class War3GameplayConfig
{
    private const int DefenseCount = 8;

    private static readonly float[] RowNormal = MiscRules.Values("Misc.DamageBonusNormal", DefenseCount);
    private static readonly float[] RowPierce = MiscRules.Values("Misc.DamageBonusPierce", DefenseCount);
    private static readonly float[] RowSiege = MiscRules.Values("Misc.DamageBonusSiege", DefenseCount);
    private static readonly float[] RowSpells = MiscRules.Values("Misc.DamageBonusSpells", DefenseCount);
    private static readonly float[] RowChaos = MiscRules.Values("Misc.DamageBonusChaos", DefenseCount);
    private static readonly float[] RowMagic = MiscRules.Values("Misc.DamageBonusMagic", DefenseCount);
    private static readonly float[] RowHero = MiscRules.Values("Misc.DamageBonusHero", DefenseCount);

    private static readonly float[] EtherealRatio = LoadEthereal();

    public static readonly float DefenseArmor = MiscRules.Value("Misc.DefenseArmor", 0.06f);

    public static readonly int HeroMaxReviveCostGold = (int)MiscRules.Value("Misc.HeroMaxReviveCostGold", 700f);
    public static readonly float HeroMaxReviveTime = MiscRules.Value("Misc.HeroMaxReviveTime", 150f);
    public static readonly float HeroReviveLifeFactor = MiscRules.Value("Misc.HeroReviveLifeFactor", 1f);
    public static readonly float HeroReviveManaStart = MiscRules.Value("Misc.HeroReviveManaStart", 1f);
    public static readonly float HeroReviveManaFactor = MiscRules.Value("Misc.HeroReviveManaFactor", 0f);
    public static readonly float ReviveBaseFactor = MiscRules.Value("Misc.ReviveBaseFactor", 0.40f);
    public static readonly float ReviveLevelFactor = MiscRules.Value("Misc.ReviveLevelFactor", 0.10f);
    public static readonly float ReviveMaxFactor = MiscRules.Value("Misc.ReviveMaxFactor", 4f);
    public static readonly float ReviveTimeFactor = MiscRules.Value("Misc.ReviveTimeFactor", 0.65f);
    public static readonly float ReviveMaxTimeFactor = MiscRules.Value("Misc.ReviveMaxTimeFactor", 2f);

    /// <summary>对标 GameplayConstants.HeroReviveGoldCost：花费 = 英雄造价 × min(Max, Base + Level×等级差)，上限 700。</summary>
    public static int HeroReviveGoldCost(int heroGoldCost, int level)
    {
        float factor = MathF.Min(ReviveMaxFactor, ReviveBaseFactor + ReviveLevelFactor * MathF.Max(0, level - 1));
        return Math.Max(0, Math.Min(HeroMaxReviveCostGold, (int)(heroGoldCost * factor)));
    }

    /// <summary>对标 NativeHeroBuildTime：时间 = 英雄训练时长 × min(等级×0.65, 2)，量化到 5 秒，上限 150。</summary>
    public static float HeroReviveSeconds(float heroTrainSeconds, int level)
    {
        float factor = MathF.Min(level * ReviveTimeFactor, ReviveMaxTimeFactor);
        float seconds = heroTrainSeconds * factor;
        seconds = MathF.Floor(seconds / 5f) * 5f;
        return MathF.Min(HeroMaxReviveTime, MathF.Max(5f, seconds));
    }

    private static float[] LoadEthereal()
    {
        float[] parsed = MiscRules.Values("Misc.EtherealDamageBonus", 7);
        for (int index = 0; index < parsed.Length; index++)
        {
            if (parsed[index] <= 0f) parsed[index] = 1f;
        }
        return parsed;
    }

    public static float ClassRatio(AttackType attack, DefenseType defense)
    {
        float[] row = attack switch
        {
            AttackType.Normal => RowNormal,
            AttackType.Pierce => RowPierce,
            AttackType.Siege => RowSiege,
            AttackType.Spells => RowSpells,
            AttackType.Chaos => RowChaos,
            AttackType.Magic => RowMagic,
            AttackType.Hero => RowHero,
            _ => null,
        };

        int column = (int)defense;
        if (row == null || column < 0 || column >= row.Length) return 1f;
        return row[column];
    }

    /// <summary>护甲减伤系数：正护甲 1/(1+k·armor)，负护甲按客户端 2-(1-k)^-armor 放大。</summary>
    public static float ArmorFactor(float armor)
    {
        if (armor >= 0f) return 1f / (1f + DefenseArmor * armor);
        return 2f - Mathf.Pow(1f - DefenseArmor, -armor);
    }

    public static float EtherealBonus(AttackType attack)
    {
        int index = (int)attack;
        return index >= 0 && index < EtherealRatio.Length ? EtherealRatio[index] : 1f;
    }

    /// <summary>
    /// 客户端结算顺序（Health.DamageScaling）：先护甲，再攻击类型×护甲类型矩阵，
    /// 最后把结果夹到不低于 min(原始伤害,1)。施工中的建筑护甲按无甲处理。
    /// </summary>
    public static float Resolve(float raw, AttackType attack, DefenseType defense, DamageType type,
        float armor, bool underConstruction, bool ethereal)
    {
        float result = raw;

        if (!underConstruction && type == DamageType.Normal) result *= ArmorFactor(armor);
        result *= ClassRatio(attack, defense);
        if (ethereal) result *= EtherealBonus(attack);

        return Mathf.Max(Mathf.Min(raw, 1f), result);
    }

    public static void LogMatrix()
    {
        GD.Print($"[COMBAT] 护甲系数 k={DefenseArmor}  虚无倍率={string.Join('/', EtherealRatio)}");
        GD.Print("[COMBAT] 倍率矩阵  行=攻击类型 列=小/中/大/加固/无甲/英雄/神圣/无");
        foreach (AttackType attack in Enum.GetValues<AttackType>())
        {
            var cells = new string[DefenseCount];
            for (int column = 0; column < DefenseCount; column++)
            {
                cells[column] = ClassRatio(attack, (DefenseType)column).ToString("0.00");
            }
            GD.Print($"[COMBAT]   {attack,-7} {string.Join("  ", cells)}");
        }
    }

    /// <summary>单次命中拆解，供探针与调试输出。</summary>
    public static string Describe(float raw, AttackType attack, DefenseType defense, DamageType type,
        float armor, bool underConstruction, bool ethereal)
    {
        float armorFactor = underConstruction || type != DamageType.Normal ? 1f : ArmorFactor(armor);
        float classRatio = ClassRatio(attack, defense);
        float resolved = Resolve(raw, attack, defense, type, armor, underConstruction, ethereal);
        return $"原始{raw:0.0} × 护甲{armor:0.0}(x{armorFactor:0.000}) × {attack}/{defense}(x{classRatio:0.00})"
            + (underConstruction ? " [施工中按无甲]" : "")
            + $" = {resolved:0.0}";
    }
}
