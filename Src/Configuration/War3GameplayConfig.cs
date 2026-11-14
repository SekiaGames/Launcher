using Godot;
using System;

using OpenWC3.Game;
namespace OpenWC3;

public sealed class War3GameplayConfig
{
    public static War3GameplayConfig Default { get; } = new();

    private const int DefenseCount = 8;

    private readonly float[] RowNormal = MiscRules.Values("Misc.DamageBonusNormal", DefenseCount);
    private readonly float[] RowPierce = MiscRules.Values("Misc.DamageBonusPierce", DefenseCount);
    private readonly float[] RowSiege = MiscRules.Values("Misc.DamageBonusSiege", DefenseCount);
    private readonly float[] RowSpells = MiscRules.Values("Misc.DamageBonusSpells", DefenseCount);
    private readonly float[] RowChaos = MiscRules.Values("Misc.DamageBonusChaos", DefenseCount);
    private readonly float[] RowMagic = MiscRules.Values("Misc.DamageBonusMagic", DefenseCount);
    private readonly float[] RowHero = MiscRules.Values("Misc.DamageBonusHero", DefenseCount);

    private readonly float[] EtherealRatio;

    public War3GameplayConfig() => EtherealRatio = LoadEthereal();

    public readonly float DefenseArmor = MiscRules.Value("Misc.DefenseArmor", 0.06f);

    public readonly float DecayTime = MiscDataValue("Misc.DecayTime", 2f);
    public readonly float StructureDecayTime = MiscDataValue("Misc.StructureDecayTime", 30f);
    public readonly float BoneDecayTime = MiscDataValue("Misc.BoneDecayTime", 88f);
    public readonly float DissipateTime = MiscDataValue("Misc.DissipateTime", 3f);

    private static float MiscDataValue(string key, float fallback)
    {
        if (!GeneratedTexts.TryGet("UnitsMiscData", key, out string text)) return fallback;
        return float.TryParse(text?.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }

    /// <summary>弹道命中后保留死亡表现的秒数（客户端 War3GameplayConfig.BulletDeathTime，MiscData 的 budt）。</summary>
    public readonly float BulletDeathTime = MiscRules.Value("Misc.BulletDeathTime", 0f);

    public readonly int HeroMaxReviveCostGold = (int)MiscRules.Value("Misc.HeroMaxReviveCostGold", 700f);
    public readonly float HeroMaxReviveTime = MiscRules.Value("Misc.HeroMaxReviveTime", 150f);
    public readonly float HeroReviveLifeFactor = MiscRules.Value("Misc.HeroReviveLifeFactor", 1f);
    public readonly float HeroReviveManaStart = MiscRules.Value("Misc.HeroReviveManaStart", 1f);
    public readonly float HeroReviveManaFactor = MiscRules.Value("Misc.HeroReviveManaFactor", 0f);
    public readonly float ReviveBaseFactor = MiscRules.Value("Misc.ReviveBaseFactor", 0.40f);
    public readonly float ReviveLevelFactor = MiscRules.Value("Misc.ReviveLevelFactor", 0.10f);
    public readonly float ReviveMaxFactor = MiscRules.Value("Misc.ReviveMaxFactor", 4f);
    public readonly float ReviveTimeFactor = MiscRules.Value("Misc.ReviveTimeFactor", 0.65f);
    public readonly float ReviveMaxTimeFactor = MiscRules.Value("Misc.ReviveMaxTimeFactor", 2f);

    // —— 维修费用（Upkeep）：客户端 War3GameplayConfig.cs:191-210 ——
    // 人口 <=50 无税，<=80 扣 30%，再高扣 60%；只扣金币，木材不扣。
    public readonly int UpkeepFoodLimitLow = (int)MiscRules.Value("Misc.UpkeepFoodLimitLow", 50f);
    public readonly int UpkeepFoodLimitHigh = (int)MiscRules.Value("Misc.UpkeepFoodLimitHigh", 80f);
    private readonly int[] UpkeepGoldTaxPct = { 0, 30, 60 };

    /// <summary>阈值相同的人口留在低一级（客户端 241d30 的口径）。</summary>
    public int UpkeepTier(int foodUsed)
    {
        if (foodUsed <= UpkeepFoodLimitLow) return 0;
        return foodUsed <= UpkeepFoodLimitHigh ? 1 : 2;
    }

    public float GoldIncomeFactor(int foodUsed) => (100 - UpkeepGoldTaxPct[UpkeepTier(foodUsed)]) / 100f;

    /// <summary>按整数百分比结算，避免 0.4f 这类浮点误差把 40 截成 39。</summary>
    public int GoldAfterUpkeep(int amount, int foodUsed) =>
        Math.Max(0, amount * (100 - UpkeepGoldTaxPct[UpkeepTier(foodUsed)]) / 100);

    /// <summary>对标 GameplayConstants.HeroReviveGoldCost：花费 = 英雄造价 × min(Max, Base + Level×等级差)，上限 700。</summary>
    public int HeroReviveGoldCost(int heroGoldCost, int level)
    {
        float factor = MathF.Min(ReviveMaxFactor, ReviveBaseFactor + ReviveLevelFactor * MathF.Max(0, level - 1));
        return Math.Max(0, Math.Min(HeroMaxReviveCostGold, (int)(heroGoldCost * factor)));
    }

    /// <summary>对标 NativeHeroBuildTime：时间 = 英雄建造时长(bldtm) × min(等级×0.65, 2)，量化到 5 秒，下限 1 秒、上限 150。</summary>
    public float HeroReviveSeconds(float heroBuildSeconds, int level)
    {
        float factor = MathF.Min(level * ReviveTimeFactor, ReviveMaxTimeFactor);
        float seconds = heroBuildSeconds * factor;
        seconds = MathF.Floor(seconds / 5f) * 5f;
        return MathF.Min(HeroMaxReviveTime, MathF.Max(1f, seconds));
    }

    private float[] LoadEthereal()
    {
        float[] parsed = MiscRules.Values("Misc.EtherealDamageBonus", 7);
        for (int index = 0; index < parsed.Length; index++)
        {
            if (parsed[index] <= 0f) parsed[index] = 1f;
        }
        return parsed;
    }

    public float ClassRatio(AttackType attack, DefenseType defense)
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
    public float ArmorFactor(float armor)
    {
        if (armor >= 0f) return 1f / (1f + DefenseArmor * armor);
        return 2f - Mathf.Pow(1f - DefenseArmor, -armor);
    }

    // —— 飘字样式（UI\MiscData + UI\MiscUI）：默认值即经典 TFT，地图可在表里覆盖 ——
    public readonly War3FloatingTextStyle CriticalStrikeText =
        War3FloatingTextStyle.Load("CriticalStrikeText", .04f, 5f, 2f, new Color(1f, 0f, 0f));
    // 普通伤害数字是「伤害数字显示」开关的玩家设置，原版表里没有对应键，只有代码默认值。
    public readonly War3FloatingTextStyle OrdinaryDamageText =
        War3FloatingTextStyle.Load("OrdinaryDamageText", .03f, .9f, .4f, new Color(1f, 1f, 1f), 1f, .018f);
    public readonly War3FloatingTextStyle MissText =
        War3FloatingTextStyle.Load("MissText", .03f, 3f, 1f, new Color(1f, 0f, 0f));
    public readonly War3FloatingTextStyle ManaBurnText =
        War3FloatingTextStyle.Load("ManaBurnText", .04f, 5f, 2f, new Color(82f / 255f, 82f / 255f, 1f));
    public readonly War3FloatingTextStyle ShadowStrikeText =
        War3FloatingTextStyle.Load("ShadowStrikeText", .04f, 5f, 2f, new Color(160f / 255f, 1f, 0f));
    public readonly War3FloatingTextStyle GoldText =
        War3FloatingTextStyle.Load("GoldText", .03f, 2f, 1f, new Color(1f, 220f / 255f, 0f));
    public readonly War3FloatingTextStyle LumberText =
        War3FloatingTextStyle.Load("LumberText", .03f, 2f, 1f, new Color(0f, 200f / 255f, 80f / 255f));
    public readonly War3FloatingTextStyle BountyText =
        War3FloatingTextStyle.Load("BountyText", .03f, 3f, 2f, new Color(1f, 220f / 255f, 0f));
    // 原版档案里没有木材赏金的键，缺省即"零字号零透明度"——也就是不显示。
    public readonly War3FloatingTextStyle LumberBountyText =
        War3FloatingTextStyle.Load("LumberBountyText", 0f, 0f, 0f, new Color(0f, 0f, 0f), 0f, 0f);

    public float EtherealBonus(AttackType attack)
    {
        int index = (int)attack;
        return index >= 0 && index < EtherealRatio.Length ? EtherealRatio[index] : 1f;
    }

    /// <summary>
    /// 客户端结算顺序（Health.DamageScaling）：先护甲，再攻击类型×护甲类型矩阵，
    /// 最后把结果夹到不低于 min(原始伤害,1)。施工中的建筑护甲按无甲处理。
    /// </summary>
    public float Resolve(float raw, AttackType attack, DefenseType defense, DamageType type,
        float armor, bool underConstruction, bool ethereal)
    {
        float result = raw;

        if (!underConstruction && type == DamageType.Normal) result *= ArmorFactor(armor);
        result *= ClassRatio(attack, defense);
        if (ethereal) result *= EtherealBonus(attack);

        return Mathf.Max(Mathf.Min(raw, 1f), result);
    }

    public void LogMatrix()
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
    public string Describe(float raw, AttackType attack, DefenseType defense, DamageType type,
        float armor, bool underConstruction, bool ethereal)
    {
        float armorFactor = underConstruction || type != DamageType.Normal ? 1f : ArmorFactor(armor);
        float classRatio = ClassRatio(attack, defense);
        float resolved = Resolve(raw, attack, defense, type, armor, underConstruction, ethereal);
        return $"原始{raw:0.0} × 护甲{armor:0.0}(x{armorFactor:0.000}) × {attack}/{defense}(x{classRatio:0.00})"
            + (underConstruction ? " [施工中按无甲]" : "")
            + $" = {resolved:0.0}";
    }

    // —— 光环授予条件（客户端 War3GameplayConfig 同名旗标，值取 Units/MiscGame.txt）——

        public int MagicImmunesResistUltimates { get; } = MiscRules.Value("Misc.MagicImmunesResistUltimates", 1f) != 0f ? 1 : 0;

    public bool EnsnareIsMagic { get; } = MiscRules.Value("Misc.EnsnareIsMagic", 1f) != 0f;

    public bool WebIsMagic { get; } = MiscRules.Value("Misc.WebIsMagic", 1f) != 0f;

    public bool DrainTransfersLife { get; } = MiscRules.Value("Misc.DrainTransfersLife", 1f) != 0f;

    public bool DrainTransfersMana { get; } = MiscRules.Value("Misc.DrainTransfersMana", 1f) != 0f;

    public bool CanDeactivateDivineShield { get; } = MiscRules.Value("Misc.CanDeactivateDivineShield", 1f) != 0f;

    public bool IllusionsGetDefenseBonus { get; } = MiscRules.Value("Misc.IllusionsGetDefenseBonus", 1f) != 0f;

    public bool IllusionsCanRestoreMana { get; } = MiscRules.Value("Misc.IllusionsCanRestoreMana", 1f) != 0f;

    public bool InvisibleUnitsBestowAuras { get; } = MiscRules.Value("Misc.InvisibleUnitsBestowAuras", 0f) != 0f;

    public bool BurrowedUnitsBestowAuras { get; } = MiscRules.Value("Misc.BurrowedUnitsBestowAuras", 0f) != 0f;

    public bool AnimatedUnitsBestowAuras { get; } = MiscRules.Value("Misc.AnimatedUnitsBestowAuras", 0f) != 0f;

    public bool FlyingHeroesBestowAuras { get; } = MiscRules.Value("Misc.FlyingHeroesBestowAuras", 0f) != 0f;

    public bool PolymorphedUnitsBestowAuras { get; } = MiscRules.Value("Misc.PolymorphedUnitsBestowAuras", 0f) != 0f;

    public bool IllusionsBestowAuras { get; } = MiscRules.Value("Misc.IllusionsBestowAuras", 0f) != 0f;
}
